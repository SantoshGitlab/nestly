using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerSkillMappingRepository : IPartnerSkillMappingRepository
{
    private readonly NestlyDbContext _context;

    public PartnerSkillMappingRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PartnerSkillMapping>> GetByPartnerAsync(Guid partnerId) =>
        await _context.Set<PartnerSkillMapping>()
            .Where(x => x.PartnerId == partnerId)
            .ToListAsync();

    public async Task ReplaceForPartnerAsync(Guid partnerId, IReadOnlyList<PartnerSkillMapping> skills)
    {
        var existing = await _context.Set<PartnerSkillMapping>()
            .Where(x => x.PartnerId == partnerId)
            .ToListAsync();
        _context.Set<PartnerSkillMapping>().RemoveRange(existing);

        await _context.Set<PartnerSkillMapping>().AddRangeAsync(skills);

        await _context.SaveChangesAsync();
    }
}
