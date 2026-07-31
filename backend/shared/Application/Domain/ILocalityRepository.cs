using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Geography master CRUD over localities (SRS 12.9.1), the finest-grained entry, tied to a pincode.</summary>
public interface ILocalityRepository : IRepository<Locality>
{
    /// <summary>Localities, optionally scoped to a zone, alphabetically ordered.</summary>
    Task<IReadOnlyList<Locality>> ListAsync(Guid? zoneId);

    /// <summary>Whether another locality in the same zone already uses this name (unique per zone).</summary>
    Task<bool> ExistsByNameInZoneAsync(Guid zoneId, string name, Guid? excludeId = null);
}
