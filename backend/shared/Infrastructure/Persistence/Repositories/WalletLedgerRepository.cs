using Microsoft.EntityFrameworkCore;
using Nestly.Application.Wallet;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class WalletLedgerRepository : IWalletLedgerRepository
{
    private readonly NestlyDbContext _context;

    public WalletLedgerRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(WalletLedgerEntry entry)
    {
        await _context.WalletLedgerEntries.AddAsync(entry);
        await _context.SaveChangesAsync();
    }

    public Task<WalletLedgerEntry?> GetLatestAsync(Guid customerId) =>
        _context.WalletLedgerEntries
            .Where(e => e.CustomerId == customerId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<WalletLedgerEntry>> ListByCustomerAsync(Guid customerId) =>
        await _context.WalletLedgerEntries
            .Where(e => e.CustomerId == customerId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<WalletLedgerEntry>> ListUnexpiredCreditsWithRemainingAsync(Guid customerId, DateTime asOfUtc) =>
        await _context.WalletLedgerEntries
            .Where(e => e.CustomerId == customerId
                && e.EntryType == WalletEntryType.Credit
                && e.ExpiresAtUtc != null && e.ExpiresAtUtc > asOfUtc
                && e.RemainingAmount > 0)
            .OrderBy(e => e.ExpiresAtUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<WalletLedgerEntry>> ListExpiredCreditsWithRemainingAsync(DateTime asOfUtc) =>
        await _context.WalletLedgerEntries
            .Where(e => e.EntryType == WalletEntryType.Credit
                && e.ExpiresAtUtc != null && e.ExpiresAtUtc <= asOfUtc
                && e.RemainingAmount > 0)
            .OrderBy(e => e.ExpiresAtUtc)
            .ToListAsync();

    public async Task UpdateRemainingAsync(WalletLedgerEntry entry)
    {
        _context.WalletLedgerEntries.Update(entry);
        await _context.SaveChangesAsync();
    }

    // Sums client-side over just the Amount column (not SumAsync) - SQLite's
    // EF provider (this repo's test suite, see TestDatabase) cannot translate
    // a SQL-side Sum over decimal, only Postgres can; this stays portable
    // across both, same reasoning BookingRepository.SearchAsync's string
    // filters already document for a different LINQ-translation gap.
    public async Task<decimal> SumCreditsBySourceTypeInRangeAsync(Guid customerId, WalletSourceType sourceType, DateTime fromUtc, DateTime toUtc) =>
        (await _context.WalletLedgerEntries
            .Where(e => e.CustomerId == customerId
                && e.SourceType == sourceType
                && e.EntryType == WalletEntryType.Credit
                && e.CreatedAtUtc >= fromUtc && e.CreatedAtUtc < toUtc)
            .Select(e => e.Amount)
            .ToListAsync())
            .Sum();

    public Task<WalletLedgerEntry?> FindBySourceAsync(WalletSourceType sourceType, Guid sourceReferenceId) =>
        _context.WalletLedgerEntries
            .FirstOrDefaultAsync(e => e.SourceType == sourceType && e.SourceReferenceId == sourceReferenceId);
}
