using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Geography master entry within a state (SRS 12.9.1). City is the level
/// category-serviceability is mapped against (SRS 12.9.2).
/// </summary>
public class City : Entity<Guid>
{
    public Guid StateId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    protected City() { }

    public City(Guid id, Guid stateId, string name) : base(id)
    {
        StateId = stateId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IsActive = true;
    }

    public void Rename(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
