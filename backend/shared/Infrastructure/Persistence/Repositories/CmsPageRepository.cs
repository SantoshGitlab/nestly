using Microsoft.EntityFrameworkCore;
using Nestly.Application.Cms;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CmsPageRepository : ICmsPageRepository
{
    private readonly NestlyDbContext _context;

    public CmsPageRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<CmsPage?> GetByIdAsync(Guid id) =>
        _context.CmsPages.FirstOrDefaultAsync(p => p.Id == id);

    public Task<CmsPage?> GetBySlugAsync(string slug) =>
        _context.CmsPages.FirstOrDefaultAsync(p => p.Slug == slug.Trim().ToLower());

    public Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null)
    {
        string normalized = slug.Trim().ToLower();
        return _context.CmsPages.AnyAsync(p => p.Slug == normalized && (!excludeId.HasValue || p.Id != excludeId.Value));
    }

    public async Task AddAsync(CmsPage page)
    {
        await _context.CmsPages.AddAsync(page);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CmsPage page)
    {
        _context.CmsPages.Update(page);
        await _context.SaveChangesAsync();
    }

    public async Task<CmsPageSearchResult> SearchAsync(CmsPageSearchFilter filter)
    {
        var query = _context.CmsPages.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Title))
        {
            string term = filter.Title.Trim();
            query = query.Where(p => p.Title.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Slug))
        {
            string term = filter.Slug.Trim().ToLower();
            query = query.Where(p => p.Slug.Contains(term));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(p => p.Status == filter.Status.Value);
        }

        if (filter.Placement.HasValue)
        {
            query = query.Where(p => p.Placement == filter.Placement.Value);
        }

        int totalCount = await query.CountAsync();

        var page = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var items = page.Select(ToResponse).ToList();
        return new CmsPageSearchResult(items, totalCount);
    }

    /// <summary>Shared with <see cref="Nestly.Infrastructure.Services.CmsPageService"/> for single-page reads.</summary>
    internal static CmsPageResponse ToResponse(CmsPage page) => new(
        page.Id,
        page.Title,
        page.Slug,
        page.Body,
        page.SeoTitle,
        page.SeoDescription,
        page.SeoKeywords,
        page.Placement,
        page.Status,
        page.PublishStartUtc,
        page.PublishEndUtc,
        page.CreatedAtUtc,
        page.UpdatedAtUtc);
}
