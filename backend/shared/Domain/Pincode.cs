using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Geography master entry within a city (SRS 12.9.1). Pincode is the level
/// service-serviceability is mapped against (SRS 12.9.2).
/// </summary>
public class Pincode : Entity<Guid>
{
    public Guid CityId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    protected Pincode() { }

    public Pincode(Guid id, Guid cityId, string code) : base(id)
    {
        CityId = cityId;
        Code = code ?? throw new ArgumentNullException(nameof(code));
        IsActive = true;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
