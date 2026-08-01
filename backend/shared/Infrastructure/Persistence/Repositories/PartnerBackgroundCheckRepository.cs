using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerBackgroundCheckRepository : IPartnerBackgroundCheckRepository
{
    private readonly NestlyDbContext _context;

    public PartnerBackgroundCheckRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PartnerBackgroundCheck entity)
    {
        await _context.PartnerBackgroundChecks.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<PartnerBackgroundCheck>> ListByPartnerAsync(Guid partnerId) =>
        await _context.PartnerBackgroundChecks
            .Where(c => c.PartnerId == partnerId)
            .OrderByDescending(c => c.CheckedAt)
            .ToListAsync();

    public Task<PartnerBackgroundCheck?> GetLatestByPartnerAsync(Guid partnerId) =>
        _context.PartnerBackgroundChecks
            .Where(c => c.PartnerId == partnerId)
            .OrderByDescending(c => c.CheckedAt)
            .FirstOrDefaultAsync();
}
