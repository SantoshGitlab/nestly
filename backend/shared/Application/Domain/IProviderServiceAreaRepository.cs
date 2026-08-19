using Nestly.Domain;

namespace Nestly.Application;

/// <summary>
/// Persistence for a provider's geography coverage (PROVIDER.md
/// "provider_service_area", task 149a "update service areas"). Replace-all
/// semantics rather than individual add/remove, mirroring
/// <c>ISlotWindowRepository.ReplaceRulesAsync</c> — a provider submits their
/// whole coverage set at once, so a full replace avoids reconciling a partial
/// diff against the unique (provider, city, zone, pincode) index.
/// </summary>
public interface IProviderServiceAreaRepository
{
    Task<IReadOnlyList<ProviderServiceArea>> GetByProviderAsync(Guid providerId);

    Task ReplaceForProviderAsync(Guid providerId, IReadOnlyList<ProviderServiceArea> areas);

    /// <summary>
    /// Active service-area city names per provider, for the admin provider
    /// directory (task 371) - a provider with no configured service areas
    /// is simply absent from the result rather than mapped to an empty list,
    /// so callers should default missing keys themselves.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ListActiveCityNamesByProviderAsync(IReadOnlyList<Guid> providerIds);
}
