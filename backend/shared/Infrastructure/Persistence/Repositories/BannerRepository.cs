using Microsoft.EntityFrameworkCore;
using Nestly.Application.Cms;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class BannerRepository : IBannerRepository
{
    private readonly NestlyDbContext _context;

    public BannerRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<Banner?> GetByIdAsync(Guid id) =>
        _context.Banners.FirstOrDefaultAsync(b => b.Id == id);

    public async Task AddAsync(Banner banner)
    {
        await _context.Banners.AddAsync(banner);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Banner banner)
    {
        _context.Banners.Update(banner);
        await _context.SaveChangesAsync();
    }

    public async Task<BannerSearchResult> SearchAsync(BannerSearchFilter filter)
    {
        var query = _context.Banners.AsQueryable();

        if (filter.Placement.HasValue)
        {
            query = query.Where(b => b.Placement == filter.Placement.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(b => b.Status == filter.Status.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(b => b.CategoryId == filter.CategoryId.Value);
        }

        int totalCount = await query.CountAsync();

        var page = await query
            .OrderBy(b => b.SortOrder)
            .ThenByDescending(b => b.CreatedAtUtc)
            .ApplyPaging(filter.Page, filter.PageSize)
            .ToListAsync();

        // Media URLs and category names resolved in one batch query each
        // rather than per-row - same N+1 avoidance CouponRepository.SearchAsync
        // uses for its category-name lookup.
        var mediaIds = page.Select(b => b.MediaId).Distinct().ToList();
        var mediaUrls = await _context.CmsMediaAssets
            .Where(m => mediaIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Url);

        var categoryIds = page.Where(b => b.CategoryId.HasValue).Select(b => b.CategoryId!.Value).Distinct().ToList();
        var categoryNames = await _context.Set<Category>()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var items = page.Select(b => ToResponse(b, mediaUrls, categoryNames)).ToList();
        return new BannerSearchResult(items, totalCount);
    }

    public async Task<IReadOnlyList<HomeBannerResponse>> ListLiveAsync(CmsPlacement placement, DateTime nowUtc)
    {
        // Mirrors Banner.IsLive, expressed as a translatable query so the
        // publish-window filter runs in the database rather than in memory.
        var live = await _context.Banners
            .Where(b => b.Placement == placement
                && b.Status == CmsContentStatus.Published
                && (b.PublishStartUtc == null || b.PublishStartUtc <= nowUtc)
                && (b.PublishEndUtc == null || b.PublishEndUtc >= nowUtc))
            .OrderBy(b => b.SortOrder)
            .ThenByDescending(b => b.CreatedAtUtc)
            // Join to media in the same round trip - the storefront needs the
            // asset URL and its alt text, and a per-row lookup would be N+1.
            .Join(
                _context.CmsMediaAssets,
                banner => banner.MediaId,
                media => media.Id,
                (banner, media) => new HomeBannerResponse(
                    banner.Id,
                    banner.Title,
                    banner.Subtitle,
                    media.Url,
                    media.AltText,
                    banner.LinkUrl))
            .ToListAsync();

        return live;
    }

    /// <summary>Shared with <see cref="Nestly.Infrastructure.Services.BannerService"/> for single-banner reads.</summary>
    internal static BannerResponse ToResponse(
        Banner banner,
        IReadOnlyDictionary<Guid, string> mediaUrls,
        IReadOnlyDictionary<Guid, string> categoryNames) => new(
        banner.Id,
        banner.Title,
        banner.Subtitle,
        banner.MediaId,
        mediaUrls.TryGetValue(banner.MediaId, out string? url) ? url : string.Empty,
        banner.LinkUrl,
        banner.Placement,
        banner.CategoryId,
        banner.CategoryId.HasValue && categoryNames.TryGetValue(banner.CategoryId.Value, out string? name) ? name : null,
        banner.SortOrder,
        banner.Status,
        banner.PublishStartUtc,
        banner.PublishEndUtc,
        banner.CreatedAtUtc,
        banner.UpdatedAtUtc);
}
