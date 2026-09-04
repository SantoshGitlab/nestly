using Nestly.Application.Cms;

namespace Nestly.Infrastructure.Services;

/// <summary>Public, storefront-facing reads over static pages - see <see cref="ICmsPageQueryService"/>.</summary>
public class CmsPageQueryService : ICmsPageQueryService
{
    private readonly ICmsPageRepository _repository;

    public CmsPageQueryService(ICmsPageRepository repository)
    {
        _repository = repository;
    }

    public async Task<CmsPageContentResponse?> GetLiveBySlugAsync(string slug)
    {
        var page = await _repository.GetBySlugAsync(slug);
        if (page is null || !page.IsLive(DateTime.UtcNow))
        {
            return null;
        }

        return new CmsPageContentResponse(page.Title, page.Slug, page.Body, page.SeoTitle, page.SeoDescription, page.UpdatedAtUtc);
    }
}
