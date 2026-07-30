using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Marks a <see cref="SlotWindow"/> as offered on a given day of the week
/// (SRS 12.10.1 "Day-of-week availability"). The time range comes from the
/// parent window - this rule only says which days it applies.
/// </summary>
public class SlotWindowRule : Entity<Guid>
{
    public Guid SlotWindowId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }

    public SlotWindow? SlotWindow { get; private set; }

    protected SlotWindowRule() { }

    public SlotWindowRule(Guid id, Guid slotWindowId, DayOfWeek dayOfWeek) : base(id)
    {
        SlotWindowId = slotWindowId;
        DayOfWeek = dayOfWeek;
    }
}
