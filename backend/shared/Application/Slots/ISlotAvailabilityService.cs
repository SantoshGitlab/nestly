using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Slots;

/// <summary>Slot availability calculation (tasks 45a-d, SRS 12.10, 24.4).</summary>
public interface ISlotAvailabilityService
{
    /// <summary>Available slots for a service, at an address (locality), on a date.</summary>
    Task<Result<SlotAvailabilityResponse>> GetAvailableSlotsAsync(Guid serviceId, Guid localityId, DateOnly date);

    /// <summary>
    /// Re-checks a previously offered slot right before booking confirmation
    /// (task 45d) - cutoff, blackout, or serviceability may have changed
    /// since the customer picked it.
    /// </summary>
    Task<Result<SlotRevalidationResponse>> RevalidateSlotAsync(Guid serviceId, Guid localityId, Guid slotWindowId, DateOnly date);
}
