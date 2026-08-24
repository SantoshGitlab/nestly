using Nestly.Domain;

namespace Nestly.Application.Cms;

public interface IBannerRepository
{
    Task<Banner?> GetByIdAsync(Guid id);

    Task AddAsync(Banner banner);

    Task UpdateAsync(Banner banner);

    /// <summary>Filtered, paginated admin banner list (SRS 12.16.1's "manage banners" screen), ordered by sort order then recency.</summary>
    Task<BannerSearchResult> SearchAsync(BannerSearchFilter filter);

    /// <summary>
    /// The banners a storefront should currently show for a placement (SRS
    /// 11.1.2/11.1.3): published and within their optional publish window at
    /// <paramref name="nowUtc"/>, ordered by sort order then recency, with the
    /// media asset resolved to URL + alt text. Empty list when none qualify.
    /// </summary>
    Task<IReadOnlyList<HomeBannerResponse>> ListLiveAsync(CmsPlacement placement, DateTime nowUtc);
}
