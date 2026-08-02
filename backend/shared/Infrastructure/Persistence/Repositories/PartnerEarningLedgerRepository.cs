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

    // Sums client-side over just the Amount column (not SumAsync) - SQLite's
    // EF provider (this repo's test suite) cannot translate a SQL-side Sum
    // over decimal, only Postgres can; mirrors WalletLedgerRepository's
    // equivalent method for the same reason.
    public async Task<decimal> SumCreditsBySourceTypeInRangeAsync(Guid partnerId, PartnerEarningSourceType sourceType, DateTime fromUtc, DateTime toUtc) =>
        (await _context.PartnerEarningLedgerEntries
            .Where(e => e.PartnerId == partnerId
                && e.SourceType == sourceType
                && e.EntryType == PartnerEarningEntryType.Credit
                && e.CreatedAtUtc >= fromUtc && e.CreatedAtUtc < toUtc)
            .Select(e => e.Amount)
            .ToListAsync())
            .Sum();

    public Task<PartnerEarningLedgerEntry?> FindBySourceAsync(PartnerEarningSourceType sourceType, Guid sourceReferenceId) =>
        _context.PartnerEarningLedgerEntries
            .FirstOrDefaultAsync(e => e.SourceType == sourceType && e.SourceReferenceId == sourceReferenceId);
}
