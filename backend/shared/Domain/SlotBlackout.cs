using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

public enum SlotBlackoutType
{
    /// <summary>A named calendar holiday (e.g. "Diwali").</summary>
    Holiday,

    /// <summary>An ad hoc admin-defined suspension (e.g. "Staff training").</summary>
    Blackout
}

/// <summary>
/// A date range in which a city is not bookable (SRS 12.10.1 "Holiday
/// blackout", 12.10.2 "block entire day"). Holidays and ad hoc blackouts
/// share the same shape - a city, a date range, and a reason - so one type
/// with a discriminator covers both rather than two near-identical tables.
/// </summary>
public class SlotBlackout : Entity<Guid>
{
    public Guid CityId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public SlotBlackoutType Type { get; private set; }
    public string? Reason { get; private set; }

    protected SlotBlackout() { }

    public SlotBlackout(Guid id, Guid cityId, DateOnly startDate, DateOnly endDate, SlotBlackoutType type, string? reason)
        : base(id)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException("The start date must not be after the end date.");
        }

        CityId = cityId;
        StartDate = startDate;
        EndDate = endDate;
        Type = type;
        Reason = reason;
    }

    /// <summary>Whether this blackout covers the given date, inclusive of both ends.</summary>
    public bool CoversDate(DateOnly date) => date >= StartDate && date <= EndDate;
}
