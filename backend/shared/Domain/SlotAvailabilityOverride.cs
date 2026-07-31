using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A one-off manual override that blocks computed slot availability for a
/// single date (SRS 12.10.2), independent of the recurring rules in
/// <see cref="SlotWindow"/>/<see cref="SlotWindowRule"/>, <see cref="SlotBlackout"/>
/// and <see cref="SlotBookingPolicy"/>. Unlike a blackout (a city-wide date
/// range, typically a holiday or planned suspension), an override targets
/// exactly one date and can be scoped as narrowly as a single slot window
/// for a single service - "Entire day", "Selected slot" and "Selected
/// city/category/service/date combination" (SRS 12.10.2) are simply how many
/// of the optional scope fields below are set: all null blocks the whole
/// city for the day, a SlotWindowId narrows to one window, and
/// Category/ServiceId narrow further still. There is no separate "blocked"
/// flag to model - every row that exists is a block, and removing it
/// (DeleteAsync) is how an admin reverses it.
/// </summary>
public class SlotAvailabilityOverride : Entity<Guid>
{
    public Guid CityId { get; private set; }
    public DateOnly Date { get; private set; }
    public Guid? SlotWindowId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? ServiceId { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    public City? City { get; private set; }
    public SlotWindow? SlotWindow { get; private set; }
    public Category? Category { get; private set; }
    public Service? Service { get; private set; }

    protected SlotAvailabilityOverride() { }

    public SlotAvailabilityOverride(
        Guid id,
        Guid cityId,
        DateOnly date,
        string reason,
        Guid? slotWindowId = null,
        Guid? categoryId = null,
        Guid? serviceId = null) : base(id)
    {
        CityId = cityId;
        Date = date;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        SlotWindowId = slotWindowId;
        CategoryId = categoryId;
        ServiceId = serviceId;
    }
}
