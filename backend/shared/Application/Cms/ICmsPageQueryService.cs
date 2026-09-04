namespace Nestly.Application.Cms;

/// <summary>
/// Public, storefront-facing reads over static pages (SRS 12.16.1/12.16.2),
/// separate from the admin <see cref="ICmsPageService"/> CRUD surface: it
/// exposes only a live, publish-windowed page's customer-safe fields, never
/// a draft or the workflow fields. Mirrors <see cref="IBannerQueryService"/>'s
/// split from its own admin service.
/// </summary>
public interface ICmsPageQueryService
{
    /// <summary>
    /// The live page at this slug, or null when no page has this slug, it is
    /// still a draft, or it has fallen outside its publish window - a
    /// customer app must not be able to tell those three apart (SRS 12.16.2).
    /// </summary>
    Task<CmsPageContentResponse?> GetLiveBySlugAsync(string slug);
}
