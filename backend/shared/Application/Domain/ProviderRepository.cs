using Nestly.Application.ProviderManagement;
using Nestly.Domain;

namespace Nestly.Application;

public interface IProviderRepository : IRepository<Provider>
{
    Task<bool> ExistsByPhoneAsync(string phone);
    Task<Provider?> GetByPhoneAsync(string phone);

    /// <summary>Search/filter with pagination for the admin provider list (task 150a) - mirrors <c>ICustomerRepository.SearchAsync</c>.</summary>
    Task<ProviderSearchResult> SearchAsync(ProviderSearchFilter filter);

    /// <summary>
    /// Display names for a page of provider ids, in one round trip (task
    /// 254) - mirrors <c>ICustomerRepository.GetNamesByIdsAsync</c>. The
    /// payout list renders a provider name per row and used to resolve them
    /// one aggregate at a time. Ids with no matching provider are absent
    /// from the result, so callers keep their own fallback.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesByIdsAsync(IReadOnlyCollection<Guid> ids);
}
