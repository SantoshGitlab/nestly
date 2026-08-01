using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Live per-day booking count against a <see cref="SlotWindow"/>'s capacity
/// (SRS 12.10.1 "if capacity is used", task 135c). One row per
/// (<see cref="SlotWindowId"/>, <see cref="SlotDate"/>) pair, created lazily
/// on the first booking made for that window+day.
///
/// This is deliberately a live operational counter, not a snapshot - unlike
/// Booking's SlotWindowId (see BookingConfiguration's comment), this row
/// exists purely to serialize concurrent capacity checks and has no history
/// value, so it carries a real foreign key to SlotWindow.
///
/// Reservation must always go through SlotCapacityRepository.TryReserveAsync,
/// which performs a single atomic conditional UPDATE - never read the count
/// and write a decision separately, or two customers racing for the last
/// seat on a promoted slot can both win.
/// </summary>
public class SlotBookingCounter : Entity<Guid>
{
    public Guid SlotWindowId { get; private set; }
    public DateOnly SlotDate { get; private set; }
    public int BookedCount { get; private set; }

    protected SlotBookingCounter() { }

    public SlotBookingCounter(Guid id, Guid slotWindowId, DateOnly slotDate, int bookedCount) : base(id)
    {
        SlotWindowId = slotWindowId;
        SlotDate = slotDate;
        BookedCount = bookedCount;
    }
}
