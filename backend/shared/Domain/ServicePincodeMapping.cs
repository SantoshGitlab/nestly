using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Serviceability mapping: whether a service is active in a given pincode
/// (SRS 12.9.2). Pincode is the level service-serviceability is mapped
/// against; a locality's serviceability follows its parent pincode.
/// Deactivating a mapping is how admins apply a temporary suspension or
/// blackout for that service in that pincode, without deleting the record.
/// </summary>
public class ServicePincodeMapping : Entity<Guid>
{
    public Guid ServiceId { get; private set; }
    public Guid PincodeId { get; private set; }
    public bool IsActive { get; private set; }

    protected ServicePincodeMapping() { }

    public ServicePincodeMapping(Guid id, Guid serviceId, Guid pincodeId) : base(id)
    {
        ServiceId = serviceId;
        PincodeId = pincodeId;
        IsActive = true;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
