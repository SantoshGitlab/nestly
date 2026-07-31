using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Immutable history record of a single booking reschedule (SRS 11.15.2,
/// 32.3, tasks 82a-d, 83). A booking may accumulate several of these up to
/// <c>ReschedulePolicyOptions.MaxReschedulesPerBooking</c> - unlike
/// <see cref="BookingCancellation"/>, there is no uniqueness constraint on
/// <see cref="BookingId"/>.
/// </summary>
public class BookingReschedule : Entity<Guid>
{
    public Guid BookingId { get; private set; }
    public RescheduleActor Actor { get; private set; }
    public string? Reason { get; private set; }

    public Guid FromSlotWindowId { get; private set; }
    public DateOnly FromSlotDate { get; private set; }
    public TimeSpan FromSlotStartTime { get; private set; }
    public TimeSpan FromSlotEndTime { get; private set; }

    public Guid ToSlotWindowId { get; private set; }
    public DateOnly ToSlotDate { get; private set; }
    public TimeSpan ToSlotStartTime { get; private set; }
    public TimeSpan ToSlotEndTime { get; private set; }

    public bool IsLate { get; private set; }
    public decimal FeeAmount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    protected BookingReschedule() { }

    public BookingReschedule(
        Guid id,
        Guid bookingId,
        RescheduleActor actor,
        string? reason,
        Guid fromSlotWindowId,
        DateOnly fromSlotDate,
        TimeSpan fromSlotStartTime,
        TimeSpan fromSlotEndTime,
        Guid toSlotWindowId,
        DateOnly toSlotDate,
        TimeSpan toSlotStartTime,
        TimeSpan toSlotEndTime,
        bool isLate,
        decimal feeAmount)
        : base(id)
    {
        if (feeAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(feeAmount), "Reschedule fee cannot be negative.");
        }

        BookingId = bookingId;
        Actor = actor;
        Reason = reason;
        FromSlotWindowId = fromSlotWindowId;
        FromSlotDate = fromSlotDate;
        FromSlotStartTime = fromSlotStartTime;
        FromSlotEndTime = fromSlotEndTime;
        ToSlotWindowId = toSlotWindowId;
        ToSlotDate = toSlotDate;
        ToSlotStartTime = toSlotStartTime;
        ToSlotEndTime = toSlotEndTime;
        IsLate = isLate;
        FeeAmount = feeAmount;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
