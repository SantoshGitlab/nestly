using Nestly.Application.Slots;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Reschedules;

/// <summary>
/// Reschedule eligibility, eligible-slot lookup, and confirmation (SRS
/// 11.15, 32.3, tasks 82a-d, 83).
/// </summary>
public interface IRescheduleService
{
    /// <summary>Eligibility summary - status, window, and count-limit checks (SRS 11.15.1).</summary>
    Task<Result<RescheduleEligibilityResponse>> GetEligibilityAsync(Guid customerId, Guid bookingId);

    /// <summary>Eligible future slots for this booking's service at <paramref name="localityId"/> on <paramref name="date"/> (SRS 11.15.3, 24.6).</summary>
    Task<Result<SlotAvailabilityResponse>> GetEligibleSlotsAsync(Guid customerId, Guid bookingId, Guid localityId, DateOnly date);

    /// <summary>Confirms the reschedule: revalidates the chosen slot, updates the booking, and records history (SRS 24.6).</summary>
    Task<Result<RescheduleOutcomeResponse>> ConfirmRescheduleAsync(Guid customerId, Guid bookingId, RescheduleBookingRequest request);

    /// <summary>
    /// Admin-initiated reschedule (SRS 12.11.3, task 117b). Not scoped to a
    /// customer id - the caller (<c>BookingManagementService</c>) has already
    /// verified the requesting admin holds "bookings.write". Still enforces
    /// the <see cref="Domain.BookingLifecycle"/> transition to
    /// <see cref="Domain.BookingStatus.Rescheduled"/> (the state-machine
    /// invariant) and still revalidates the new slot's live availability
    /// through <see cref="Application.Slots.ISlotAvailabilityService"/> (a
    /// slot that filled up between lookup and confirmation must still be
    /// rejected for an admin exactly as it would be for a customer) - but
    /// deliberately skips the customer-protection policy limits
    /// (<c>ReschedulePolicyOptions.MaxReschedulesPerBooking</c>,
    /// <c>MinHoursBeforeSlot</c>): those exist to stop a customer from
    /// abusing self-service rescheduling, not to stop an admin from acting on
    /// a customer's behalf (e.g. a support call to move a booking past the
    /// self-service cutoff) - the entire reason this is a distinct action
    /// from the customer-facing one, rather than the same method with a
    /// different actor label.
    /// </summary>
    Task<Result<RescheduleOutcomeResponse>> AdminRescheduleAsync(Guid bookingId, RescheduleBookingRequest request);
}
