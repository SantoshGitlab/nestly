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

    /// <summary>
    /// Atomically reserves one seat against the window's per-day capacity
    /// (SlotWindow.MaxBookingsPerSlot, SRS 12.10.1, task 135c). Must be
    /// called once, immediately before a booking is persisted for that
    /// window+date - never treat a prior <see cref="RevalidateSlotAsync"/>
    /// success as proof capacity is still available, since another booking
    /// may have taken the last seat in between. A window with no configured
    /// capacity (null) always succeeds without reserving anything.
    /// </summary>
    Task<Result> ReserveSlotAsync(Guid slotWindowId, DateOnly date);
}
