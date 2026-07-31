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
}
