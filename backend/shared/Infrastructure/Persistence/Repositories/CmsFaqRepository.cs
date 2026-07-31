using Microsoft.EntityFrameworkCore;
using Nestly.Application.Cms;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CmsFaqRepository : ICmsFaqRepository
{
    private readonly NestlyDbContext _context;

    public CmsFaqRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<CmsFaq?> GetByIdAsync(Guid id) =>
        _context.CmsFaqs.FirstOrDefaultAsync(f => f.Id == id);

    public async Task AddAsync(CmsFaq faq)
    {
        await _context.CmsFaqs.AddAsync(faq);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CmsFaq faq)
    {
        _context.CmsFaqs.Update(faq);
        await _context.SaveChangesAsync();
    }

    public async Task<CmsFaqSearchResult> SearchAsync(CmsFaqSearchFilter filter)
    {
        var query = _context.CmsFaqs.AsQueryable();

        if (filter.Placement.HasValue)
        {
            query = query.Where(f => f.Placement == filter.Placement.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(f => f.Status == filter.Status.Value);
        }

        int totalCount = await query.CountAsync();

        var page = await query
            .OrderBy(f => f.SortOrder)
            .ThenByDescending(f => f.CreatedAtUtc)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var items = page.Select(ToResponse).ToList();
        return new CmsFaqSearchResult(items, totalCount);
    }

    /// <summary>Shared with <see cref="Nestly.Infrastructure.Services.CmsFaqService"/> for single-FAQ reads.</summary>
    internal static CmsFaqResponse ToResponse(CmsFaq faq) => new(
        faq.Id,
        faq.Question,
        faq.Answer,
        faq.Placement,
        faq.SortOrder,
        faq.Status,
        faq.PublishStartUtc,
        faq.PublishEndUtc,
        faq.CreatedAtUtc,
        faq.UpdatedAtUtc);
}
