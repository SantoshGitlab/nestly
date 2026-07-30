using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Per-city booking constraints (SRS 12.10.1): how soon before a slot a
/// booking can still be made, and how far ahead one can be made. One row
/// per city - both are simple scalar settings with no sub-structure, so
/// they share a single policy record rather than two near-empty tables.
/// </summary>
public class SlotBookingPolicy : Entity<Guid>
{
    public Guid CityId { get; private set; }

    /// <summary>Minimum lead time before a slot starts (SRS 12.10.1 "Cutoff rules").</summary>
    public int CutoffMinutes { get; private set; }

    /// <summary>How many days ahead a slot can be booked (SRS 12.10.1 "Advance booking limit").</summary>
    public int MaxAdvanceDays { get; private set; }

    protected SlotBookingPolicy() { }

    public SlotBookingPolicy(Guid id, Guid cityId, int cutoffMinutes, int maxAdvanceDays) : base(id)
    {
        CityId = cityId;
        SetCutoffMinutes(cutoffMinutes);
        SetMaxAdvanceDays(maxAdvanceDays);
    }

    public void SetCutoffMinutes(int cutoffMinutes)
    {
        CutoffMinutes = cutoffMinutes >= 0
            ? cutoffMinutes
            : throw new ArgumentOutOfRangeException(nameof(cutoffMinutes), "Cutoff minutes cannot be negative.");
    }

    public void SetMaxAdvanceDays(int maxAdvanceDays)
    {
        MaxAdvanceDays = maxAdvanceDays > 0
            ? maxAdvanceDays
            : throw new ArgumentOutOfRangeException(nameof(maxAdvanceDays), "Max advance days must be positive.");
    }
}
