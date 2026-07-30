using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// A recurring time-of-day booking window for a city (SRS 12.10.1), e.g.
/// "Morning" 09:00-13:00. Which days of the week it's actually offered on
/// is expressed by its <see cref="SlotWindowRule"/> children, not here -
/// a window with no rules is configured but not yet scheduled on any day.
/// </summary>
public class SlotWindow : Entity<Guid>
{
    public Guid CityId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Max bookings this window can hold per day (SRS 12.10.1 "if capacity is used"). Null = unlimited.</summary>
    public int? MaxBookingsPerSlot { get; private set; }

    public City? City { get; private set; }

    protected SlotWindow() { }

    public SlotWindow(Guid id, Guid cityId, string name, TimeSpan startTime, TimeSpan endTime) : base(id)
    {
        CityId = cityId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SetTimes(startTime, endTime);
        IsActive = true;
    }

    public void SetTimes(TimeSpan startTime, TimeSpan endTime)
    {
        if (startTime >= endTime)
        {
            throw new ArgumentException("The start time must be before the end time.");
        }

        StartTime = startTime;
        EndTime = endTime;
    }

    public void SetName(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

    public void SetCapacity(int? maxBookingsPerSlot)
    {
        if (maxBookingsPerSlot is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBookingsPerSlot), "Capacity must be positive when set.");
        }

        MaxBookingsPerSlot = maxBookingsPerSlot;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
