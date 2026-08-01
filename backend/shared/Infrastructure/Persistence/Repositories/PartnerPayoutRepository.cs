using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerPayoutRepository : IPartnerPayoutRepository
{
    private readonly NestlyDbContext _context;

    public PartnerPayoutRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PartnerPayout entity)
    {
        await _context.PartnerPayouts.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PartnerPayout entity)
    {
        if (_context.Entry(entity).State == EntityState.Detached)
        {
            _context.PartnerPayouts.Update(entity);
        }

        await _context.SaveChangesAsync();
    }

    public Task<PartnerPayout?> GetByIdAsync(Guid id) =>
        _context.PartnerPayouts.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IReadOnlyList<PartnerPayout>> ListByPartnerAsync(Guid partnerId) =>
        await _context.PartnerPayouts
            .Where(p => p.PartnerId == partnerId)
            .OrderByDescending(p => p.PeriodStart)
            .ToListAsync();

    public async Task<(IReadOnlyList<PartnerPayout> Rows, int TotalCount)> SearchAsync(Guid? partnerId, PartnerPayoutStatus? status, int page, int pageSize)
    {
        var query = _context.PartnerPayouts.AsQueryable();

        if (partnerId.HasValue)
        {
            query = query.Where(p => p.PartnerId == partnerId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        int totalCount = await query.CountAsync();

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (rows, totalCount);
    }
}
