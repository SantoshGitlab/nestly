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
}
