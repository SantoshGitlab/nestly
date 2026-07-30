using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Represents a rule for slot windows based on the day of the week.
/// </summary>
public class SlotWindowRule : Entity<Guid>
{
    public Guid SlotWindowId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeSpan Start { get; private set; }
    public TimeSpan End { get; private set; }

    public SlotWindow? SlotWindow { get; private set; }

    protected SlotWindowRule() { }

    public SlotWindowRule(Guid id, Guid slotWindowId, DayOfWeek dayOfWeek, TimeSpan start, TimeSpan end)
        : base(id)
    {
        SlotWindowId = slotWindowId;
        DayOfWeek = dayOfWeek;
        Start = start;
        End = end;

        Validate();
    }

    private void Validate()
    {
        if (Start >= End)
            throw new ArgumentException("The start time must be before the end time.");
    }
}
