using Microsoft.Extensions.Options;
using Nestly.Application.Abstractions.Time;
using Nestly.Application.Bookings;
using Nestly.Application.Payments;
using Nestly.Application.Refunds;
using Nestly.Application.Reschedules;
using Nestly.Application.Slots;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Reschedule eligibility, eligible-slot lookup, and confirmation (SRS
/// 11.15, 32.3, tasks 82a-d, 83). Status eligibility is derived from
/// <see cref="BookingLifecycle"/> the same way <see cref="CancellationService"/>
/// derives cancellation eligibility - see <see cref="Booking.Reschedule"/>.
/// New-slot availability is re-checked through <see cref="ISlotAvailabilityService"/>
/// (Phase 2's slot engine) at confirmation time, never trusted from an
/// earlier lookup (task 82c).
/// </summary>
public class RescheduleService : IRescheduleService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly IRefundTransactionRepository _refundTransactionRepository;
    private readonly ISlotAvailabilityService _slotAvailabilityService;
    private readonly IRescheduleRepository _rescheduleRepository;
    private readonly IBusinessClock _businessClock;
    private readonly TimeProvider _timeProvider;
    private readonly ReschedulePolicyOptions _policy;

    public RescheduleService(
        IBookingRepository bookingRepository,
        IPaymentTransactionRepository paymentRepository,
        IRefundTransactionRepository refundTransactionRepository,
        ISlotAvailabilityService slotAvailabilityService,
        IRescheduleRepository rescheduleRepository,
        IBusinessClock businessClock,
        TimeProvider timeProvider,
        IOptions<ReschedulePolicyOptions> policy)
    {
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _refundTransactionRepository = refundTransactionRepository;
        _slotAvailabilityService = slotAvailabilityService;
        _rescheduleRepository = rescheduleRepository;
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

        decimal payableAmount = await ResolvePayableAmountAsync(booking.Id);
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

    /// <summary>What remains on the booking's payment - 0 if it was never paid for or already fully refunded (same rule CancellationService uses).</summary>
    private async Task<decimal> ResolvePayableAmountAsync(Guid bookingId)
    {
        var payment = await _paymentRepository.GetByBookingIdAsync(bookingId);
        if (payment is null || payment.Status != PaymentTransactionStatus.Success)
        {
            return 0m;
        }

        var priorRefunds = await _refundTransactionRepository.ListByPaymentTransactionAsync(payment.Id);
        decimal alreadyRefunded = priorRefunds.Where(r => r.Status != RefundStatus.Failed).Sum(r => r.Amount);
        decimal remaining = payment.Amount - alreadyRefunded;
        return remaining > 0 ? remaining : 0m;
    }
}
