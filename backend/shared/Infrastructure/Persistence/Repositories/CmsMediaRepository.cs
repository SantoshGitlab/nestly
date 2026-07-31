using Microsoft.EntityFrameworkCore;
using Nestly.Application.Cms;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CmsMediaRepository : ICmsMediaRepository
{
    private readonly NestlyDbContext _context;

    public CmsMediaRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<CmsMedia?> GetByIdAsync(Guid id) =>
        _context.CmsMediaAssets.FirstOrDefaultAsync(m => m.Id == id);

    public async Task<IReadOnlyList<CmsMedia>> ListAsync() =>
        await _context.CmsMediaAssets.OrderByDescending(m => m.CreatedAtUtc).ToListAsync();

    public async Task AddAsync(CmsMedia media)
    {
        await _context.CmsMediaAssets.AddAsync(media);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CmsMedia media)
    {
        _context.CmsMediaAssets.Update(media);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(CmsMedia media)
    {
        _context.CmsMediaAssets.Remove(media);
        await _context.SaveChangesAsync();
    }

    public Task<bool> IsReferencedByBannerAsync(Guid mediaId) =>
        _context.Banners.AnyAsync(b => b.MediaId == mediaId);
}
