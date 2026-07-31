using Microsoft.EntityFrameworkCore;
using Nestly.Application.Escrow;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PlatformEscrowLedgerRepository : IPlatformEscrowLedgerRepository
{
    private readonly NestlyDbContext _context;

    public PlatformEscrowLedgerRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PlatformEscrowLedger entry)
    {
        await _context.PlatformEscrowLedgers.AddAsync(entry);
        await _context.SaveChangesAsync();
    }

    public Task<PlatformEscrowLedger?> GetLatestAsync() =>
        _context.PlatformEscrowLedgers
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<PlatformEscrowLedger>> ListByBookingAsync(Guid bookingId) =>
        await _context.PlatformEscrowLedgers
            .Where(e => e.BookingId == bookingId)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync();
}
