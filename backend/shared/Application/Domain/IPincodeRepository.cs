using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Geography master CRUD over pincodes (SRS 12.9.1). Pincode is the level service-serviceability is mapped against (SRS 12.9.2).</summary>
public interface IPincodeRepository : IRepository<Pincode>
{
    /// <summary>Pincodes, optionally scoped to a city, ordered by code.</summary>
    Task<IReadOnlyList<Pincode>> ListAsync(Guid? cityId);

    /// <summary>Whether another pincode already uses this code (Pincode.Code is globally unique).</summary>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null);
}
