namespace Nestly.Application.Cms;

/// <summary>
/// Public, storefront-facing reads over banners (SRS 11.1.2/11.1.3), separate
/// from the admin <see cref="IBannerService"/> CRUD surface: it exposes only
/// the live, publish-windowed banners a customer app renders, never drafts or
/// workflow fields. Mirrors the split between <c>ICategoryQueryService</c>
/// (consumer) and the admin category management service.
/// </summary>
public interface IBannerQueryService
{
    /// <summary>The banners currently live in the Home placement, ordered for display. Empty when none qualify.</summary>
    Task<IReadOnlyList<HomeBannerResponse>> ListLiveHomeBannersAsync();
}
