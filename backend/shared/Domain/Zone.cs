using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Operational grouping of localities within a city (SRS 12.9.1), used to
/// scope slot capacity and blackout rules below city level.
/// </summary>
public class Zone : Entity<Guid>
{
    public Guid CityId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    protected Zone() { }

    public Zone(Guid id, Guid cityId, string name) : base(id)
    {
        CityId = cityId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IsActive = true;
    }

    public void Rename(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
