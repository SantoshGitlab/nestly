using Nestly.Application.Serviceability;
using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Admin CRUD over the service/pincode serviceability mapping (SRS 12.9.2).</summary>
public interface IServicePincodeMappingRepository : IRepository<ServicePincodeMapping>
{
    /// <summary>
    /// Mappings joined with service/pincode details for display, optionally
    /// filtered by service and/or pincode. Joined here rather than resolved
    /// per-row by the caller, to avoid N+1 lookups on a listing endpoint.
    /// </summary>
    Task<IReadOnlyList<ServicePincodeMappingResponse>> ListAsync(Guid? serviceId, Guid? pincodeId);

    /// <summary>The existing mapping for this (service, pincode) pair, if any - the pair is unique.</summary>
    Task<ServicePincodeMapping?> FindAsync(Guid serviceId, Guid pincodeId);
}
