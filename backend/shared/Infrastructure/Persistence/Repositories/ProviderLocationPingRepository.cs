using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ProviderLocationPingRepository : IProviderLocationPingRepository
{
    private readonly NestlyDbContext _context;

    public ProviderLocationPingRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ProviderLocationPing entity)
    {
        await _context.Set<ProviderLocationPing>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    // Both reads order by ReceivedAtUtc after RecordedAtUtc: two fixes can
    // carry the same device timestamp (a device reporting whole seconds, or a
    // retried upload), and without a tie-break "the latest fix" would be
    // whichever row the database happened to return first.
    public Task<ProviderLocationPing?> GetLatestForBookingAsync(Guid bookingId) =>
        _context.Set<ProviderLocationPing>()
            .Where(x => x.BookingId == bookingId)
            .OrderByDescending(x => x.RecordedAtUtc)
            .ThenByDescending(x => x.ReceivedAtUtc)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<ProviderLocationPing>> GetTrailForBookingAsync(Guid bookingId) =>
        await _context.Set<ProviderLocationPing>()
            .Where(x => x.BookingId == bookingId)
            .OrderBy(x => x.RecordedAtUtc)
            .ThenBy(x => x.ReceivedAtUtc)
            .ToListAsync();
}
