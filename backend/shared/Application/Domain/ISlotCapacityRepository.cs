namespace Nestly.Application;

/// <summary>
/// Enforces SlotWindow.MaxBookingsPerSlot (SRS 12.10.1, task 135c). Not an
/// <see cref="IRepository{T}"/> - SlotBookingCounter is a purely internal
/// concurrency-control row with no read/list use case of its own, so there is
/// no generic CRUD surface worth exposing for it.
/// </summary>
public interface ISlotCapacityRepository
{
    /// <summary>
    /// Atomically reserves one seat against a slot window's per-day
    /// capacity. Returns <c>false</c> without reserving anything if the slot
    /// is already at <paramref name="maxCapacity"/> for that day. Safe under
    /// concurrent callers racing for the same window+day - see
    /// SlotCapacityRepository's doc comment for how.
    /// </summary>
    Task<bool> TryReserveAsync(Guid slotWindowId, DateOnly date, int maxCapacity);
}
