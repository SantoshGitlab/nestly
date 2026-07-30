using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Finest-grained geography master entry (SRS 12.9.1): a named locality
/// within a zone, tied to the pincode it falls under for serviceability
/// mapping (SRS 12.9.2).
/// </summary>
public class Locality : Entity<Guid>
{
    public Guid ZoneId { get; private set; }
    public Guid PincodeId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public Zone? Zone { get; private set; }
    public Pincode? Pincode { get; private set; }

    protected Locality() { }

    public Locality(Guid id, Guid zoneId, Guid pincodeId, string name) : base(id)
    {
        ZoneId = zoneId;
        PincodeId = pincodeId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IsActive = true;
    }

    public void Rename(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
