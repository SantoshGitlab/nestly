using Nestly.Application.Cms;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Public, storefront-facing reads over banners (SRS 11.1.2/11.1.3) - see <see cref="IBannerQueryService"/>.</summary>
public class BannerQueryService : IBannerQueryService
{
    private readonly IBannerRepository _bannerRepository;

    public BannerQueryService(IBannerRepository bannerRepository)
    {
        _bannerRepository = bannerRepository;
    }

    public Task<IReadOnlyList<HomeBannerResponse>> ListLiveHomeBannersAsync() =>
        _bannerRepository.ListLiveAsync(CmsPlacement.Home, DateTime.UtcNow);
}
