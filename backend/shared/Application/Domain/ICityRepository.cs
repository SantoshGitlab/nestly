using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Geography master CRUD over cities (SRS 12.9.1). City is the level category-serviceability is mapped against (SRS 12.9.2).</summary>
public interface ICityRepository : IRepository<City>
{
    /// <summary>Cities, optionally scoped to a state, alphabetically ordered.</summary>
    Task<IReadOnlyList<City>> ListAsync(Guid? stateId);

    /// <summary>Whether another city in the same state already uses this name (unique per state).</summary>
    Task<bool> ExistsByNameInStateAsync(Guid stateId, string name, Guid? excludeId = null);

    /// <summary>Names for a set of city ids in one round trip (task 256) - mirrors <c>ICustomerRepository.GetNamesByIdsAsync</c>. The admin blackout/policy/override lists each render a city name per row.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids);
}
