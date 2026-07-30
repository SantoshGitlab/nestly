using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Represents a time window during which slots can be booked.
/// </summary>
public class SlotWindow : Entity<Guid>
{
    public Guid CityId { get; private set; }
    public DateTime StartDateTime { get; private set; }
    public DateTime EndDateTime { get; private set; }

    public City? City { get; private set; }

    protected SlotWindow() { }

    public SlotWindow(Guid id, Guid cityId, DateTime startDateTime, DateTime endDateTime)
        : base(id)
    {
        CityId = cityId;
        StartDateTime = startDateTime;
        EndDateTime = endDateTime;

        Validate();
    }

    /// <summary>
    /// Validates the slot window's properties.
    /// </summary>
    private void Validate()
    {
        if (StartDateTime >= EndDateTime)
        {
            throw new ArgumentException("The start date and time must be before the end date and time.");
        }
    }

    public void Update(DateTime? startDateTime = null, DateTime? endDateTime = null)
    {
        if (startDateTime.HasValue) StartDateTime = startDateTime.Value;
        if (endDateTime.HasValue) EndDateTime = endDateTime.Value;

        Validate();
    }
}
