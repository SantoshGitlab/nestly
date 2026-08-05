namespace Nestly.Application;

/// <summary>
/// Enforces SlotWindow.MaxBookingsPerSlot (SRS 12.10.1, task 135c). Not an
/// <see cref="IRepository{T}"/> - SlotBookingCounter is a purely internal
/// concurrency-control row with no read/list use case of its own, so there is
/// no generic CRUD surface worth exposing for it.
///
/// Reservations are strictly paired: every <see cref="TryReserveAsync"/> that
/// returns true is owned by exactly one booking on that window+day, and must
/// be handed back through <see cref="ReleaseAsync"/> when that booking stops
/// occupying the slot (cancelled, or rescheduled onto a different slot).
/// Without the release half the counter only ever grows, and a window
/// silently runs out of capacity while its seats sit with bookings that no
/// longer exist.
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

    /// <summary>
    /// Atomically hands one seat back. A no-op when no counter row exists or
    /// the count is already zero, so a double release (a cancellation retried
    /// after a partial failure, say) can never drive the counter negative and
    /// oversell the slot.
    /// </summary>
    Task ReleaseAsync(Guid slotWindowId, DateOnly date);

    /// <summary>
    /// Seats already taken on <paramref name="date"/>, keyed by slot window,
    /// for the windows asked about. Windows with no counter row yet are
    /// absent from the result rather than present with zero. One query for
    /// the whole set - slot availability asks about every window in a city at
    /// once, and a per-window round trip would be an N+1 on the hottest read
    /// path in the funnel.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetBookedCountsAsync(IReadOnlyCollection<Guid> slotWindowIds, DateOnly date);
}
