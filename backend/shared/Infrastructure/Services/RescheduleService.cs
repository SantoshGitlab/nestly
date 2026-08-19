using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.Abstractions.Time;
using Nestly.Application.Bookings;
using Nestly.Application.Payments;
using Nestly.Application.ProviderManagement;
using Nestly.Application.Refunds;
using Nestly.Application.Reschedules;
using Nestly.Application.Slots;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Reschedule eligibility, eligible-slot lookup, and confirmation (SRS
/// 11.15, 32.3, tasks 82a-d, 83). Status eligibility is derived from
/// <see cref="BookingLifecycle"/> the same way <see cref="CancellationService"/>
/// derives cancellation eligibility - see <see cref="Booking.Reschedule"/>.
/// New-slot availability is re-checked through <see cref="ISlotAvailabilityService"/>
/// (Phase 2's slot engine) at confirmation time, never trusted from an
/// earlier lookup (task 82c).
///
/// <para>
/// <b>Task 290 - a reschedule does not automatically drop the assigned
/// provider.</b> <see cref="Booking.Reschedule"/> only moves the slot; it
/// never touches <see cref="Booking.AssignedProviderId"/> or the live
/// <c>BookingProviderAssignment</c> row (see that method's own doc comment,
/// corrected by this task). <see cref="ReconcileProviderAssignmentAfterRescheduleAsync"/>
/// is what decides, after every reschedule, whether the same professional
/// stays on the job: kept when the new slot is still free for them (reusing
/// <see cref="IProviderScheduleConflictService"/>, the exact predicate task
/// 288 built for initial assignment, rather than a second one), dropped back
/// to <see cref="BookingStatus.AwaitingFulfilment"/> - <c>Reschedule</c>'s
/// own destination - when it now conflicts with another job.
/// </para>
/// </summary>
public class RescheduleService : IRescheduleService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly IRefundTransactionRepository _refundTransactionRepository;
    private readonly ISlotAvailabilityService _slotAvailabilityService;
    private readonly IRescheduleRepository _rescheduleRepository;
    private readonly IBookingProviderAssignmentRepository _assignmentRepository;
    private readonly IProviderScheduleConflictService _scheduleConflictService;
    private readonly NestlyDbContext _context;
    private readonly IBusinessClock _businessClock;
    private readonly TimeProvider _timeProvider;
    private readonly ReschedulePolicyOptions _policy;

    public RescheduleService(
        IBookingRepository bookingRepository,
        IPaymentTransactionRepository paymentRepository,
        IRefundTransactionRepository refundTransactionRepository,
        ISlotAvailabilityService slotAvailabilityService,
        IRescheduleRepository rescheduleRepository,
        IBookingProviderAssignmentRepository assignmentRepository,
        IProviderScheduleConflictService scheduleConflictService,
        NestlyDbContext context,
        IBusinessClock businessClock,
        TimeProvider timeProvider,
        IOptions<ReschedulePolicyOptions> policy)
    {
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _refundTransactionRepository = refundTransactionRepository;
        _slotAvailabilityService = slotAvailabilityService;
        _rescheduleRepository = rescheduleRepository;
        _assignmentRepository = assignmentRepository;
        _scheduleConflictService = scheduleConflictService;
        _context = context;
        _businessClock = businessClock;
        _timeProvider = timeProvider;
        _policy = policy.Value;
    }

    public async Task<Result<RescheduleEligibilityResponse>> GetEligibilityAsync(Guid customerId, Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("Reschedule.BookingNotFound", "The specified booking does not exist.");
        }

        return Result.Success(await EvaluateEligibilityAsync(booking));
    }

    public async Task<Result<SlotAvailabilityResponse>> GetEligibleSlotsAsync(Guid customerId, Guid bookingId, Guid localityId, DateOnly date)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("Reschedule.BookingNotFound", "The specified booking does not exist.");
        }

        var eligibility = await EvaluateEligibilityAsync(booking);
        if (!eligibility.IsEligible)
        {
            return Error.Business("Reschedule.NotEligible", eligibility.IneligibilityReason!);
        }

        Guid serviceId = booking.Items.Count > 0
            ? booking.Items[0].ServiceId
            : throw new InvalidOperationException($"Booking {bookingId} has no items to resolve a service from.");

        return await _slotAvailabilityService.GetAvailableSlotsAsync(serviceId, localityId, date);
    }

    public async Task<Result<RescheduleOutcomeResponse>> ConfirmRescheduleAsync(Guid customerId, Guid bookingId, RescheduleBookingRequest request)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("Reschedule.BookingNotFound", "The specified booking does not exist.");
        }

        var eligibility = await EvaluateEligibilityAsync(booking);
        if (!eligibility.IsEligible)
        {
            return Error.Business("Reschedule.NotEligible", eligibility.IneligibilityReason!);
        }

        return await ExecuteRescheduleAsync(booking, request, RescheduleActor.Customer);
    }

    public async Task<Result<RescheduleOutcomeResponse>> AdminRescheduleAsync(Guid bookingId, RescheduleBookingRequest request)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("Reschedule.BookingNotFound", "The specified booking does not exist.");
        }

        // Only the state-machine invariant is enforced here - the count-limit
        // and min-hours-before-slot policy checks in EvaluateEligibilityAsync
        // are deliberately skipped, see this method's doc comment on
        // IRescheduleService.
        if (!BookingLifecycle.IsValidTransition(booking.Status, BookingStatus.Rescheduled))
        {
            return Error.Business("Reschedule.NotEligible", $"A booking in status '{booking.Status}' cannot be rescheduled.");
        }

        return await ExecuteRescheduleAsync(booking, request, RescheduleActor.Admin);
    }

    /// <summary>
    /// Shared confirmation path for both <see cref="ConfirmRescheduleAsync"/>
    /// and <see cref="AdminRescheduleAsync"/>: revalidates the chosen slot
    /// through the slot engine (task 82c - never trusted from an earlier
    /// lookup, for either actor), computes the late-reschedule fee via
    /// <see cref="RescheduleFeeCalculator"/>, applies the booking-level
    /// transition, and records the <see cref="BookingReschedule"/> history
    /// row attributed to <paramref name="actor"/>.
    /// </summary>
    private async Task<Result<RescheduleOutcomeResponse>> ExecuteRescheduleAsync(Booking booking, RescheduleBookingRequest request, RescheduleActor actor)
    {
        Guid serviceId = booking.Items.Count > 0
            ? booking.Items[0].ServiceId
            : throw new InvalidOperationException($"Booking {booking.Id} has no items to resolve a service from.");

        // Slot revalidation via the slot engine (task 82c): the exact same
        // computation RevalidateSlotAsync performs, but this also returns
        // the slot's own name/time details needed for the new snapshot.
        var availability = await _slotAvailabilityService.GetAvailableSlotsAsync(serviceId, request.LocalityId, request.SlotDate);
        if (availability.IsFailure)
        {
            return availability.Error;
        }

        var chosenSlot = availability.Value.Slots.FirstOrDefault(s => s.SlotWindowId == request.SlotWindowId);
        if (!availability.Value.IsServiceable || chosenSlot is null)
        {
            return Error.Business("Reschedule.SlotNotAvailable", "The selected slot is no longer available.");
        }

        var previousSlot = new BookingSlotSummary(booking.SlotWindowId, booking.SlotWindowNameSnapshot, booking.SlotDate, booking.SlotStartTimeSnapshot, booking.SlotEndTimeSnapshot);

        bool movingSlot = previousSlot.SlotWindowId != chosenSlot.SlotWindowId || previousSlot.Date != request.SlotDate;

        // Take a seat on the target slot before giving up the current one.
        // Availability above only reports what is free; it does not hold
        // anything, so without this reservation a reschedule could move any
        // number of bookings onto a capped window - the cap was only ever
        // enforced on the create path (BookingService.CreateAsync).
        if (movingSlot)
        {
            var reservation = await _slotAvailabilityService.ReserveSlotAsync(chosenSlot.SlotWindowId, request.SlotDate);
            if (reservation.IsFailure)
            {
                return reservation.Error;
            }
        }

        decimal payableAmount = await ResolvePayableAmountAsync(booking);
        // The snapshot is a business wall-clock time; lifting it to a real
        // instant is what makes "how long until the slot" comparable with UTC
        // now (see IBusinessClock) - subtracting the two directly skewed every
        // late-reschedule fee decision by the business timezone's offset.
        DateTime currentSlotStartUtc = _businessClock.ToUtc(booking.SlotDate, booking.SlotStartTimeSnapshot);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        var feeOutcome = RescheduleFeeCalculator.Compute(
            payableAmount, currentSlotStartUtc - now, _policy.LateFeeThresholdHours, _policy.LateRescheduleFeePercentage);

        booking.Reschedule(chosenSlot.SlotWindowId, request.SlotDate, chosenSlot.Name, chosenSlot.StartTime, chosenSlot.EndTime, request.Reason);
        await _bookingRepository.UpdateAsync(booking);

        // Task 290: the slot move above always persists regardless of what
        // happens to the assignment - only "keep the same professional" can
        // fail, and it must never take the reschedule itself down with it.
        booking = await ReconcileProviderAssignmentAfterRescheduleAsync(booking);

        // Only once the move is committed: releasing first would let a
        // concurrent booking take the seat this reschedule might still need to
        // roll back to.
        if (movingSlot)
        {
            await _slotAvailabilityService.ReleaseSlotAsync(previousSlot.SlotWindowId, previousSlot.Date);
        }

        var history = new BookingReschedule(
            Guid.NewGuid(),
            booking.Id,
            actor,
            request.Reason,
            previousSlot.SlotWindowId,
            previousSlot.Date,
            previousSlot.StartTime,
            previousSlot.EndTime,
            chosenSlot.SlotWindowId,
            request.SlotDate,
            chosenSlot.StartTime,
            chosenSlot.EndTime,
            feeOutcome.IsLate,
            feeOutcome.FeeAmount);

        await _rescheduleRepository.AddAsync(history);

        int reschedulesUsed = await _rescheduleRepository.CountByBookingAsync(booking.Id);

        return Result.Success(new RescheduleOutcomeResponse(
            booking.Id,
            booking.Status,
            previousSlot,
            new BookingSlotSummary(chosenSlot.SlotWindowId, chosenSlot.Name, request.SlotDate, chosenSlot.StartTime, chosenSlot.EndTime),
            feeOutcome.IsLate,
            feeOutcome.FeeAmount,
            reschedulesUsed,
            _policy.MaxReschedulesPerBooking,
            history.CreatedAtUtc));
    }

    /// <summary>
    /// Task 290. Called after the slot move has already been persisted -
    /// decides what happens to the booking's live provider assignment
    /// against the *new* slot (already set on <paramref name="booking"/> by
    /// this point, which is what makes <see cref="IProviderScheduleConflictService.FindConflictAsync"/>'s
    /// own self-exclusion correct here rather than comparing against the old
    /// slot). Returns the booking to keep using - normally the same instance,
    /// but a fresh reload after the race-losing branch below.
    /// </summary>
    private async Task<Booking> ReconcileProviderAssignmentAfterRescheduleAsync(Booking booking)
    {
        if (booking.AssignedProviderId is not { } providerId)
        {
            return booking;
        }

        var activeAssignment = await _assignmentRepository.GetActiveByBookingAsync(booking.Id);
        if (activeAssignment is null)
        {
            // AssignedProviderId set with no backing live assignment row -
            // a stale display field, not something this task's fix should
            // carry forward silently.
            booking.AssignProvider(null);
            await _bookingRepository.UpdateAsync(booking);
            return booking;
        }

        // Task 288's own reasoning applies verbatim: the read this decides on
        // and the write that acts on it must not be split by a concurrent
        // assignment on the same connection. See BookingProviderAssignmentService's
        // class doc comment for what Serializable does and does not guarantee
        // per database provider (Postgres' ex_booking_provider_no_double_booking
        // exclusion constraint is the backstop caught below; SQLite has neither).
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var conflict = await _scheduleConflictService.FindConflictAsync(providerId, booking);

        try
        {
            if (conflict is null)
            {
                // Only announce the status when the booking is not already
                // sitting on it. Booking.Reschedule leaves it at
                // AwaitingFulfilment, and the UpdateAsync above dispatches
                // that BookingStatusChangedEvent to ProviderAutoAssignmentHandler,
                // which runs in-process and synchronously - so by the time
                // this method is reached the booking has frequently already
                // been promoted to Assigned by the auto-assigner (that is
                // also why AssignedProviderId is set at all). BookingLifecycle
                // deliberately has no Assigned -> Assigned self-edge, so
                // re-announcing it throws InvalidOperationException - which
                // the catch below does not handle (it is scoped to
                // DbUpdateException) and which therefore escaped as a 500,
                // breaking the guarantee ExecuteRescheduleAsync states above:
                // only "keep the same professional" may fail, never the
                // reschedule itself. Leaving it Assigned is exactly the
                // intended end state either way.
                if (booking.Status != BookingStatus.Assigned)
                {
                    booking.TransitionTo(BookingStatus.Assigned, "Reschedule kept the assigned professional; the new slot is still free for them.");
                }
            }
            else
            {
                activeAssignment.Withdraw();
                await _assignmentRepository.UpdateAsync(activeAssignment);
                booking.AssignProvider(null);
            }

            await _bookingRepository.UpdateAsync(booking);
            await dbTransaction.CommitAsync();
            return booking;
        }
        catch (DbUpdateException)
        {
            // Either the exclusion constraint rejected the write or the
            // serializable transaction lost a race - a competing assignment
            // committed between the conflict check above and this write.
            // The slot move already persisted before this method ran; only
            // "keep the same professional" failed, so drop the assignment
            // and leave the booking needing reassignment rather than
            // surfacing this as a raw 500 to the caller.
            await dbTransaction.RollbackAsync();
            DetachPendingAssignmentWrites();

            var freshBooking = await _bookingRepository.GetByIdAsync(booking.Id)
                ?? throw new InvalidOperationException($"Booking {booking.Id} disappeared mid-reschedule.");
            var freshAssignment = await _assignmentRepository.GetActiveByBookingAsync(booking.Id);
            if (freshAssignment is not null)
            {
                freshAssignment.Withdraw();
                await _assignmentRepository.UpdateAsync(freshAssignment);
            }

            freshBooking.AssignProvider(null);
            if (freshBooking.Status == BookingStatus.Assigned)
            {
                freshBooking.TransitionTo(BookingStatus.AwaitingFulfilment, "Reassignment needed - the professional was double-booked by a concurrent change.");
            }

            await _bookingRepository.UpdateAsync(freshBooking);
            return freshBooking;
        }
    }

    private void DetachPendingAssignmentWrites()
    {
        var pending = _context.ChangeTracker.Entries()
            .Where(e => e.Entity is BookingProviderAssignment or Booking)
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();

        foreach (var entry in pending)
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task<RescheduleEligibilityResponse> EvaluateEligibilityAsync(Booking booking)
    {
        if (!BookingLifecycle.IsValidTransition(booking.Status, BookingStatus.Rescheduled))
        {
            return new RescheduleEligibilityResponse(
                false, $"A booking in status '{booking.Status}' cannot be rescheduled.", 0, _policy.MaxReschedulesPerBooking, _policy.MinHoursBeforeSlot);
        }

        int reschedulesUsed = await _rescheduleRepository.CountByBookingAsync(booking.Id);
        if (reschedulesUsed >= _policy.MaxReschedulesPerBooking)
        {
            return new RescheduleEligibilityResponse(
                false, $"This booking has already been rescheduled the maximum of {_policy.MaxReschedulesPerBooking} time(s).",
                reschedulesUsed, _policy.MaxReschedulesPerBooking, _policy.MinHoursBeforeSlot);
        }

        // Business wall-clock lifted to a real instant before meeting UTC now
        // - see IBusinessClock and the same correction in ExecuteRescheduleAsync.
        DateTime slotStartUtc = _businessClock.ToUtc(booking.SlotDate, booking.SlotStartTimeSnapshot);
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        double hoursUntilSlot = (slotStartUtc - now).TotalHours;

        if (hoursUntilSlot < (double)_policy.MinHoursBeforeSlot)
        {
            return new RescheduleEligibilityResponse(
                false, "The reschedule window for this booking's slot has expired.", reschedulesUsed, _policy.MaxReschedulesPerBooking, _policy.MinHoursBeforeSlot);
        }

        return new RescheduleEligibilityResponse(true, null, reschedulesUsed, _policy.MaxReschedulesPerBooking, _policy.MinHoursBeforeSlot);
    }

    /// <summary>
    /// What the booking is still funded by, and therefore the base the
    /// late-reschedule fee percentage applies against - 0 if it was never
    /// funded at all (a 100%-off coupon, a free subscription visit, an AMC
    /// redemption) or has already been fully refunded.
    ///
    /// <para>
    /// Task 364: this is the gateway payment PLUS the wallet balance the
    /// booking consumed at checkout, computed by the same
    /// <see cref="RefundAllocationCalculator"/> <c>CancellationService</c>
    /// charges its fee against and <c>RefundService</c> allocates against, so
    /// no two of the three can compute a fee off a different base. Reading
    /// only the gateway payment (as this did before) understated a part-wallet
    /// booking's recorded late-reschedule fee by exactly the wallet-funded
    /// share, and zeroed it outright on a fully wallet-covered booking, which
    /// has no <see cref="PaymentTransaction"/> at all (task 331).
    /// </para>
    ///
    /// <para>
    /// Prior refunds are listed by booking rather than by payment transaction:
    /// task 356 made a wallet-funded refund a row with no
    /// <c>PaymentTransactionId</c>, so the by-payment query cannot see one and
    /// would count wallet money already returned as still refundable.
    /// </para>
    /// </summary>
    private async Task<decimal> ResolvePayableAmountAsync(Booking booking)
    {
        var payment = await _paymentRepository.GetByBookingIdAsync(booking.Id);
        decimal paymentSettledAmount = payment is { Status: PaymentTransactionStatus.Success } ? payment.Amount : 0m;
        var priorRefunds = await _refundTransactionRepository.ListByBookingAsync(booking.Id);

        return RefundAllocationCalculator
            .ComputeRemaining(paymentSettledAmount, booking.WalletCreditAppliedSnapshot ?? 0m, priorRefunds)
            .Total;
    }
}
