using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerServiceAreaRepository : IPartnerServiceAreaRepository
{
    private readonly NestlyDbContext _context;

    public PartnerServiceAreaRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PartnerServiceArea>> GetByPartnerAsync(Guid partnerId) =>
        await _context.Set<PartnerServiceArea>()
            .Where(x => x.PartnerId == partnerId)
            .ToListAsync();

    public async Task ReplaceForPartnerAsync(Guid partnerId, IReadOnlyList<PartnerServiceArea> areas)
    {
        var existing = await _context.Set<PartnerServiceArea>()
            .Where(x => x.PartnerId == partnerId)
            .ToListAsync();
        _context.Set<PartnerServiceArea>().RemoveRange(existing);

        await _context.Set<PartnerServiceArea>().AddRangeAsync(areas);

        await _context.SaveChangesAsync();
    }
}
