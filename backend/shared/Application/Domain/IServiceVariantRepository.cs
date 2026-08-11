using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Admin CRUD + read access over a service's priced/timed variants (Phase 3 catalog redesign).</summary>
public interface IServiceVariantRepository : IRepository<ServiceVariant>
{
    Task DeleteAsync(ServiceVariant entity);

    /// <summary>Every variant of a service regardless of active status, ordered for the admin management screen.</summary>
    Task<IReadOnlyList<ServiceVariant>> ListByServiceAsync(Guid serviceId);

    /// <summary>
    /// Active variants across every service in <paramref name="serviceIds"/>,
    /// in one query - the batch counterpart to <see cref="ListByServiceAsync"/>
    /// for callers assembling a detail response, mirroring
    /// <c>IServiceAddOnRepository.ListActiveByServiceIdsAsync</c>.
    /// </summary>
    Task<IReadOnlyList<ServiceVariant>> ListActiveByServiceIdsAsync(IReadOnlyCollection<Guid> serviceIds);
}
