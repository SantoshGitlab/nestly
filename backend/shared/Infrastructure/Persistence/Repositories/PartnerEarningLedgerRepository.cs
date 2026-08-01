using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerEarningLedgerRepository : IPartnerEarningLedgerRepository
{
    private readonly NestlyDbContext _context;

    public PartnerEarningLedgerRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PartnerEarningLedgerEntry entry)
    {
        await _context.PartnerEarningLedgerEntries.AddAsync(entry);
        await _context.SaveChangesAsync();
    }

    public Task<PartnerEarningLedgerEntry?> GetLatestAsync(Guid partnerId) =>
        _context.PartnerEarningLedgerEntries
            .Where(e => e.PartnerId == partnerId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<PartnerEarningLedgerEntry>> ListByPartnerAsync(Guid partnerId) =>
        await _context.PartnerEarningLedgerEntries
            .Where(e => e.PartnerId == partnerId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<PartnerEarningLedgerEntry>> ListByPartnerAndPeriodAsync(Guid partnerId, DateOnly periodStart, DateOnly periodEnd)
    {
        var startUtc = periodStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = periodEnd.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _context.PartnerEarningLedgerEntries
            .Where(e => e.PartnerId == partnerId && e.CreatedAtUtc >= startUtc && e.CreatedAtUtc <= endUtc)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync();
    }
}
