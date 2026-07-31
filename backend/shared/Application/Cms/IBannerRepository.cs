using Nestly.Domain;

namespace Nestly.Application.Cms;

public interface IBannerRepository
{
    Task<Banner?> GetByIdAsync(Guid id);

    Task AddAsync(Banner banner);

    Task UpdateAsync(Banner banner);

    /// <summary>Filtered, paginated admin banner list (SRS 12.16.1's "manage banners" screen), ordered by sort order then recency.</summary>
    Task<BannerSearchResult> SearchAsync(BannerSearchFilter filter);
}
