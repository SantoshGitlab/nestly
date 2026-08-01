using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerAvailabilityWindowRepository : IPartnerAvailabilityWindowRepository
{
    private readonly NestlyDbContext _context;

    public PartnerAvailabilityWindowRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PartnerAvailabilityWindow>> GetByPartnerAsync(Guid partnerId)
    {
        // Ordered client-side: SQLite (used by the test suite) cannot
        // translate an ORDER BY over a TimeSpan column, and PostgreSQL's
        // "interval" ordering would differ subtly enough from .NET's
        // TimeSpan comparison that doing it once, consistently, in memory is
        // simpler than relying on the provider.
        var windows = await _context.Set<PartnerAvailabilityWindow>()
            .Where(x => x.PartnerId == partnerId)
            .ToListAsync();

        return windows.OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).ToList();
    }

    public async Task ReplaceForPartnerAsync(Guid partnerId, IReadOnlyList<PartnerAvailabilityWindow> windows)
    {
        var existing = await _context.Set<PartnerAvailabilityWindow>()
            .Where(x => x.PartnerId == partnerId)
            .ToListAsync();
        _context.Set<PartnerAvailabilityWindow>().RemoveRange(existing);

        await _context.Set<PartnerAvailabilityWindow>().AddRangeAsync(windows);

        await _context.SaveChangesAsync();
    }
}
