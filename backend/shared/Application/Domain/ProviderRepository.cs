using Nestly.Application.ProviderManagement;
using Nestly.Domain;

namespace Nestly.Application;

public interface IProviderRepository : IRepository<Provider>
{
    Task<bool> ExistsByPhoneAsync(string phone);
    Task<Provider?> GetByPhoneAsync(string phone);

    /// <summary>Task 372: email-uniqueness check for provider registration, mirroring <c>ICustomerRepository.ExistsByEmailAsync</c>.</summary>
    Task<bool> ExistsByEmailAsync(string email);

    /// <summary>Provider-referral code uniqueness check, mirroring <c>ICustomerRepository.ExistsByReferralCodeAsync</c>.</summary>
    Task<bool> ExistsByReferralCodeAsync(string referralCode);

    /// <summary>Resolves a shared referral code back to the referrer, mirroring <c>ICustomerRepository.GetByReferralCodeAsync</c>.</summary>
    Task<Provider?> GetByReferralCodeAsync(string referralCode);

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

    /// <summary>
    /// Task 293: the admin photo-moderation queue - every provider whose
    /// profile photo is still <see cref="ProviderPhotoModerationStatus.Pending"/>,
    /// oldest submission first so nothing starves at the back of it.
    /// Unpaginated: this is a work queue meant to be emptied, not a
    /// directory, and if it ever grows large enough to need paging that is a
    /// staffing signal rather than a query problem.
    /// </summary>
    Task<IReadOnlyList<Provider>> ListPendingPhotoModerationAsync(CancellationToken cancellationToken = default);
}
