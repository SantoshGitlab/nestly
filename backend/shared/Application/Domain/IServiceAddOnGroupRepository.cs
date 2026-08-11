using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Admin CRUD + read access over a service's add-on groups and their selection rules (Phase 3 catalog redesign).</summary>
public interface IServiceAddOnGroupRepository : IRepository<ServiceAddOnGroup>
{
    Task DeleteAsync(ServiceAddOnGroup entity);

    /// <summary>Every add-on group, optionally filtered to one service, ordered for the admin management screen.</summary>
    Task<IReadOnlyList<ServiceAddOnGroup>> ListAllAsync(Guid? serviceId);

    /// <summary>
    /// Groups across every service in <paramref name="serviceIds"/>, in one
    /// query - the batch counterpart to <see cref="ListAllAsync"/> for
    /// callers assembling a detail response. No active/inactive concept
    /// exists on this entity (unlike <see cref="ServiceVariant"/>), so this
    /// returns every group for the given services.
    /// </summary>
    Task<IReadOnlyList<ServiceAddOnGroup>> ListByServiceIdsAsync(IReadOnlyCollection<Guid> serviceIds);

    /// <summary>
    /// Groups for a set of ids in one round trip, keyed by id - used by
    /// PriceCalculationService to validate selected add-ons against their
    /// group's selection rule without a query per group.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ServiceAddOnGroup>> GetByIdsAsync(IReadOnlyCollection<Guid> ids);
}
