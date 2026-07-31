using Nestly.Domain;

namespace Nestly.Application.Cms;

public interface ICmsPageRepository
{
    Task<CmsPage?> GetByIdAsync(Guid id);

    Task<CmsPage?> GetBySlugAsync(string slug);

    /// <summary>Whether a page already exists with this slug (normalized the same way as the constructor), optionally excluding one id - used to reject duplicate slugs on create/edit.</summary>
    Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null);

    Task AddAsync(CmsPage page);

    Task UpdateAsync(CmsPage page);

    /// <summary>Filtered, paginated admin page list (SRS 12.16.1's "manage pages" screen).</summary>
    Task<CmsPageSearchResult> SearchAsync(CmsPageSearchFilter filter);
}
