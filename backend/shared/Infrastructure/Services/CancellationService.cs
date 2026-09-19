using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Abstractions.Time;
using Nestly.Application.Bookings;
using Nestly.Application.Cancellations;
using Nestly.Application.Coupons;
using Nestly.Application.Escrow;
using Nestly.Application.Payments;
using Nestly.Application.Refunds;
using Nestly.Application.Slots;
using Nestly.Application.Subscriptions;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Cancellation eligibility, fee/refund computation, and confirmation (SRS
/// 11.14, 32.2, tasks 80a-c, 81). Eligibility is derived entirely from
/// <see cref="BookingLifecycle"/> - a booking can be cancelled by the
/// customer exactly when the lifecycle already allows the transition to
/// <see cref="BookingStatus.CancelledByCustomer"/>, which naturally encodes
/// "not already cancelled/completed" and "service hasn't started yet"
/// (InProgress only allows CancelledByAdmin) without duplicating that rule
/// here.
///
/// Fee/refund computation reuses <see cref="CancellationFeeCalculator"/> for
/// the pure math and <see cref="IRefundService"/> (Phase 4) for actually
/// raising the refund - this service never talks to the payment gateway or
/// wallet directly. <see cref="IEscrowService"/> is the one exception: it is
/// purely an internal bookkeeping ledger, not a real money movement, and
/// this is the only place that knows how much of a cancellation's refund
/// was withheld as a fee - see <see cref="EscrowSourceType.CancellationFeeRetained"/>.
/// </summary>
public class CancellationService : ICancellationService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly IRefundTransactionRepository _refundTransactionRepository;
    private readonly IRefundService _refundService;
    private readonly ICancellationRepository _cancellationRepository;
    private readonly IBookingProviderAssignmentRepository _assignmentRepository;
    private readonly ISlotAvailabilityService _slotAvailabilityService;
    private readonly ICouponService _couponService;
    private readonly ICustomerSubscriptionRepository _customerSubscriptionRepository;
    private readonly IEscrowService _escrowService;
    private readonly IBusinessClock _businessClock;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationPolicyOptions _policy;

    public CancellationService(
        IBookingRepository bookingRepository,
        IPaymentTransactionRepository paymentRepository,
        IRefundTransactionRepository refundTransactionRepository,
        IRefundService refundService,
        ICancellationRepository cancellationRepository,
        IBookingProviderAssignmentRepository assignmentRepository,
        ISlotAvailabilityService slotAvailabilityService,
        ICouponService couponService,
        ICustomerSubscriptionRepository customerSubscriptionRepository,
        IEscrowService escrowService,
        IBusinessClock businessClock,
        TimeProvider timeProvider,
        IOptions<CancellationPolicyOptions> policy)
    {
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _refundTransactionRepository = refundTransactionRepository;
        _refundService = refundService;
        _cancellationRepository = cancellationRepository;
        _assignmentRepository = assignmentRepository;
        _slotAvailabilityService = slotAvailabilityService;
        _couponService = couponService;
        _customerSubscriptionRepository = customerSubscriptionRepository;
        _escrowService = escrowService;
        _businessClock = businessClock;
        _timeProvider = timeProvider;
        _policy = policy.Value;
    }

    public async Task<Result<CancellationPolicyResponse>> GetPolicyAsync(Guid customerId, Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("Cancellation.BookingNotFound", "The specified booking does not exist.");
        }

        bool eligible = BookingLifecycle.IsValidTransition(booking.Status, BookingStatus.CancelledByCustomer);
        if (!eligible)
        {
            return Result.Success(new CancellationPolicyResponse(
                IsEligible: false,
                IneligibilityReason: $"A booking in status '{booking.Status}' can no longer be cancelled by the customer.",
                WithinFreeCancellationWindow: false,
                CancellationFeeAmount: 0m,
                RefundAmount: 0m,
                RefundMethod: RefundMethod.Gateway,
                _policy.FreeCancellationWindowHours,
                _policy.LateCancellationFeePercentage));
        }

        var outcome = await ComputeOutcomeAsync(booking);

        return Result.Success(new CancellationPolicyResponse(
            IsEligible: true,
            IneligibilityReason: null,
            outcome.WithinFreeWindow,
            outcome.FeeAmount,
            outcome.RefundAmount,
            RefundMethod.Gateway,
            _policy.FreeCancellationWindowHours,
            _policy.LateCancellationFeePercentage));
    }

    public async Task<Result<CancellationOutcomeResponse>> CancelAsync(Guid customerId, Guid bookingId, CancelBookingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error.Validation("Cancellation.ReasonRequired", "A cancellation reason is required.");
        }

        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("Cancellation.BookingNotFound", "The specified booking does not exist.");
        }

        if (!BookingLifecycle.IsValidTransition(booking.Status, BookingStatus.CancelledByCustomer))
        {
            return Error.Business(
                "Cancellation.NotEligible",
                $"A booking in status '{booking.Status}' can no longer be cancelled by the customer.");
        }

        return await ExecuteCancellationAsync(booking, BookingStatus.CancelledByCustomer, CancellationActor.Customer, request.Reason, internalNotes: null);
    }

    public async Task<Result<CancellationOutcomeResponse>> AdminCancelAsync(Guid bookingId, string reason, string? internalNotes = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("Cancellation.ReasonRequired", "A cancellation reason is required.");
        }

        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("Cancellation.BookingNotFound", "The specified booking does not exist.");
        }

        if (!BookingLifecycle.IsValidTransition(booking.Status, BookingStatus.CancelledByAdmin))
        {
            return Error.Business(
                "Cancellation.NotEligible",
                $"A booking in status '{booking.Status}' can no longer be cancelled.");
        }

        return await ExecuteCancellationAsync(booking, BookingStatus.CancelledByAdmin, CancellationActor.Admin, reason, internalNotes);
    }

    /// <summary>
    /// Shared confirmation path for both <see cref="CancelAsync"/> and
    /// <see cref="AdminCancelAsync"/>: computes the fee/refund outcome via
    /// <see cref="CancellationFeeCalculator"/> (same policy math for either
    /// actor - task 117a composes this rather than reimplementing it),
    /// transitions the booking, raises a refund if one is owed, and records
    /// the <see cref="BookingCancellation"/> history row.
    /// </summary>
    private async Task<Result<CancellationOutcomeResponse>> ExecuteCancellationAsync(
        Booking booking, BookingStatus targetStatus, CancellationActor actor, string reason, string? internalNotes)
    {
        var outcome = await ComputeOutcomeAsync(booking);

        // NESTLY-002: reserve this booking's one-and-only BookingCancellation
        // row (unique index on BookingId, BookingCancellationConfiguration)
        // *before* transitioning the booking or ever calling IRefundService -
        // the same TryAddAsync/"loser reads the winner's row" idiom
        // PaymentService.CreateOrderAsync already uses for the identical
        // duplicate-concurrent-request race on PaymentTransaction. Two
        // near-simultaneous cancel calls for the same booking (double-click,
        // client retry, two open tabs) both read the same pre-cancellation
        // booking and both pass the lifecycle check above; without an atomic
        // reservation here, both would independently reach IRefundService,
        // which itself only sees "nothing refunded yet" from each request's
        // own point of view since neither has committed - so both would
        // actually move money. Refund fields are attached below once the
        // winner's own refund (if any) has actually been raised.
        var cancellation = new BookingCancellation(
            Guid.NewGuid(),
            booking.Id,
            actor,
            reason,
            outcome.WithinFreeWindow,
            outcome.FeeAmount,
            outcome.RefundAmount,
            refundMethod: null,
            refundTransactionId: null,
            internalNotes);

        if (!await _cancellationRepository.TryAddAsync(cancellation))
        {
            return await BuildAlreadyCancelledResultAsync(booking);
        }

        booking.TransitionTo(targetStatus, reason);
        await _bookingRepository.UpdateAsync(booking);

        // Hand the slot's seat back to the pool. The reservation was taken
        // when the booking was created (BookingService.CreateAsync); without
        // this release the counter only ever climbs, and a window fills up
        // with cancelled bookings until it rejects real customers while
        // standing empty. Released after the transition so a booking that
        // could not legally be cancelled never frees a seat it still holds.
        await _slotAvailabilityService.ReleaseSlotAsync(booking.SlotWindowId, booking.SlotDate);

        // A booking cancelled before it was ever actually paid for (still
        // PaymentPending - BookingLifecycle allows cancelling straight from
        // there) never reaches IRefundService below, since there is no real
        // settled payment to refund. Without this, a coupon reserved and
        // redeemed at checkout (BookingService.CreateAsync) stayed permanently
        // burned even though the order it was "used" on never happened -
        // the same leak BookingExpirySweepJob had for the timeout case.
        // Gated on "never settled" specifically, not "refund amount is zero":
        // a genuinely paid-then-fully-refunded booking's coupon usage is a
        // separate policy question, not this leak, and is left untouched.
        var settledPayment = await _paymentRepository.GetByBookingIdAsync(booking.Id);
        if (settledPayment is not { Status: PaymentTransactionStatus.Success })
        {
            await _couponService.ReleaseAsync(booking.Id);
        }

        // A subscription-funded free visit (SubscriptionFreeVisitApplied)
        // is never gated on payment status the way a coupon is above - a
        // free-visit booking always has FinalPayable forced to zero
        // (SubscriptionBenefitService.PreviewAsync), so it goes straight to
        // Confirmed and never has a settled payment to check either way.
        // Without this, cancelling such a booking - an everyday action, not
        // an edge case - permanently lost that period's free-visit credit
        // for a service that was never rendered, the same leak class the
        // coupon release above fixes.
        if (booking.SubscriptionId is { } subscriptionId && booking.SubscriptionFreeVisitApplied)
        {
            await _customerSubscriptionRepository.ReleaseFreeVisitAsync(subscriptionId);
        }

        // Task 208: a provider still Assigned/Accepted on this booking has no
        // way of hearing about the cancellation otherwise - ProviderJobService
        // derives their job status from this assignment row, not from the
        // booking itself.
        var activeAssignment = await _assignmentRepository.GetActiveByBookingAsync(booking.Id);
        if (activeAssignment is not null)
        {
            activeAssignment.Withdraw();
            await _assignmentRepository.UpdateAsync(activeAssignment);
        }

        Guid? refundTransactionId = null;
        RefundStatus? refundStatus = null;
        RefundMethod? refundMethod = null;

        if (outcome.RefundAmount > 0)
        {
            var refundResult = outcome.FeeAmount > 0
                ? await _refundService.InitiatePartialRefundAsync(booking.Id, outcome.RefundAmount, reason)
                : await _refundService.InitiateFullRefundAsync(booking.Id, reason);

            if (refundResult.IsFailure)
            {
                return refundResult.Error;
            }

            // Task 356: a booking funded from both the gateway and the
            // customer's wallet settles as two refund rows. BookingCancellation
            // records one reference - the payment-funded one
            // (RefundOutcomeResponse.Primary) - and the full amount is already
            // carried by its own RefundAmount column; the other settlement is
            // reachable through the booking's refund history either way.
            refundTransactionId = refundResult.Value.Primary.Id;
            refundStatus = refundResult.Value.Primary.Status;
            refundMethod = refundResult.Value.Primary.Method;

            cancellation.AttachRefund(refundTransactionId.Value, refundMethod.Value);
            await _cancellationRepository.UpdateAsync(cancellation);
        }

        // A late-cancellation fee is platform revenue, not a refund -
        // ReleaseForRefundAsync above only ever releases outcome.RefundAmount
        // (or never runs at all, when the fee consumes the entire payable
        // amount and RefundAmount is zero), so the fee's own share stays
        // "held" in escrow with nothing else that will ever claim it: this
        // booking is now terminal (Cancelled, never Completed), so
        // EscrowReleaseOnCompletionHandler can never run for it either.
        // Independent of the refund branch above on purpose - it must still
        // run when the fee consumes the whole amount and there is no refund
        // to raise at all.
        if (outcome.FeeAmount > 0)
        {
            await _escrowService.ReleaseRetainedFeeAsync(booking.Id, cancellation.Id, outcome.FeeAmount);
        }

        // Re-fetch to report the booking's true post-refund status: a full
        // refund with nothing owed moves the booking straight past
        // CancelledByCustomer/CancelledByAdmin to RefundPending/Refunded
        // inside IRefundService.
        var finalBooking = await _bookingRepository.GetByIdAsync(booking.Id) ?? booking;

        return Result.Success(new CancellationOutcomeResponse(
            booking.Id,
            finalBooking.Status,
            outcome.WithinFreeWindow,
            outcome.FeeAmount,
            outcome.RefundAmount,
            refundStatus,
            refundMethod,
            refundTransactionId,
            cancellation.CreatedAtUtc));
    }

    /// <summary>
    /// NESTLY-002: the losing side of the TryAddAsync race above - some
    /// concurrent request already reserved (and, per <see cref="BookingCancellation.RefundTransactionId"/>,
    /// possibly already completed) this booking's cancellation. Reports that
    /// winner's outcome instead of touching the booking or IRefundService
    /// again, so a duplicate request reads as a clean no-op rather than a
    /// second real cancellation/refund.
    /// </summary>
    private async Task<Result<CancellationOutcomeResponse>> BuildAlreadyCancelledResultAsync(Booking booking)
    {
        var winner = await _cancellationRepository.GetByBookingIdAsync(booking.Id)
            ?? throw new InvalidOperationException($"Booking {booking.Id} lost its cancellation reservation race but no winning row was found.");

        var winnerBooking = await _bookingRepository.GetByIdAsync(booking.Id) ?? booking;
        var winnerRefund = winner.RefundTransactionId is Guid refundId
            ? await _refundTransactionRepository.GetByIdAsync(refundId)
            : null;

        return Result.Success(new CancellationOutcomeResponse(
            booking.Id,
            winnerBooking.Status,
            winner.WithinFreeCancellationWindow,
            winner.CancellationFeeAmount,
            winner.RefundAmount,
            winnerRefund?.Status,
            winner.RefundMethod,
            winner.RefundTransactionId,
            winner.CreatedAtUtc));
    }

    private async Task<CancellationFeeCalculator.Outcome> ComputeOutcomeAsync(Booking booking)
    {
        decimal payableAmount = await ResolveRefundableAmountAsync(booking);

        // Business wall-clock lifted to a real instant before it meets UTC
        // now - see IBusinessClock. Subtracting the raw snapshot skewed
        // "am I inside the free-cancellation window" by the business
        // timezone's offset, which in IST handed out 5.5 hours of free
        // cancellation nobody had earned.
        DateTime slotStartUtc = _businessClock.ToUtc(booking.SlotDate, booking.SlotStartTimeSnapshot);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        TimeSpan timeUntilSlot = slotStartUtc - now;

        var outcome = CancellationFeeCalculator.Compute(
            payableAmount, timeUntilSlot, _policy.FreeCancellationWindowHours, _policy.LateCancellationFeePercentage);

        // Floor at whatever a prior reschedule already locked in (see
        // Booking.LockedCancellationFeeSnapshot) - the live computation above
        // is only against the *current* slot, which a reschedule can move
        // arbitrarily far out. Without this floor, rescheduling a booking
        // that already owed a late-cancellation fee to a distant slot and
        // then immediately cancelling would compute a full refund, erasing a
        // fee that was already earned on the slot given up.
        if (booking.LockedCancellationFeeSnapshot > outcome.FeeAmount)
        {
            decimal fee = Math.Min(booking.LockedCancellationFeeSnapshot, payableAmount);
            outcome = new CancellationFeeCalculator.Outcome(false, fee, payableAmount - fee);
        }

        return outcome;
    }

    /// <summary>
    /// What the customer still stands to get back - 0 if the booking was
    /// never funded at all (a 100%-off coupon, a free subscription visit, an
    /// AMC redemption) or has already been fully refunded. Task 356: this is
    /// the gateway payment PLUS the wallet balance the booking consumed at
    /// checkout, computed by the same <see cref="RefundAllocationCalculator"/>
    /// <c>RefundService</c> allocates against, so the fee this service charges
    /// and the refund that service raises can never be computed off different
    /// bases. Reading only the payment (as this did before) charged a
    /// part-wallet booking's late-cancellation fee against the gateway half
    /// alone and then left the wallet half unrefunded entirely.
    /// </summary>
    private async Task<decimal> ResolveRefundableAmountAsync(Booking booking)
    {
        var payment = await _paymentRepository.GetByBookingIdAsync(booking.Id);
        decimal paymentSettledAmount = payment is { Status: PaymentTransactionStatus.Success } ? payment.Amount : 0m;
        var priorRefunds = await _refundTransactionRepository.ListByBookingAsync(booking.Id);

        return RefundAllocationCalculator
            .ComputeRemaining(paymentSettledAmount, booking.WalletCreditAppliedSnapshot ?? 0m, priorRefunds)
            .Total;
    }
}
