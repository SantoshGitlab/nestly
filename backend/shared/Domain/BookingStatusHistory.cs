using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// An append-only record of a single booking status change (SRS 13.2 -
/// "status history shall be stored separately", task 55d/56c). Never
/// updated or deleted once written; <see cref="Booking"/> is the only thing
/// that creates rows here, through <see cref="Booking.TransitionTo"/>.
/// </summary>
public class BookingStatusHistory : Entity<Guid>
{
    public Guid BookingId { get; private set; }

    /// <summary>Null only for the first row, recording the booking's initial status.</summary>
    public BookingStatus? FromStatus { get; private set; }

    public BookingStatus ToStatus { get; private set; }
    public string? Reason { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    protected BookingStatusHistory() { }

    public BookingStatusHistory(Guid id, Guid bookingId, BookingStatus? fromStatus, BookingStatus toStatus, string? reason, DateTime changedAtUtc)
        : base(id)
    {
        BookingId = bookingId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Reason = reason;
        ChangedAtUtc = changedAtUtc;
    }
}
