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
}
