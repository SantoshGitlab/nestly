using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Geography master CRUD over zones (SRS 12.9.1).</summary>
public interface IZoneRepository : IRepository<Zone>
{
    /// <summary>Zones, optionally scoped to a city, alphabetically ordered.</summary>
    Task<IReadOnlyList<Zone>> ListAsync(Guid? cityId);

    /// <summary>Whether another zone in the same city already uses this name (unique per city).</summary>
    Task<bool> ExistsByNameInCityAsync(Guid cityId, string name, Guid? excludeId = null);
}
