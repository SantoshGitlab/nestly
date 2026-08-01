using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PartnerBlackoutDateRepository : IPartnerBlackoutDateRepository
{
    private readonly NestlyDbContext _context;

    public PartnerBlackoutDateRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PartnerBlackoutDate>> GetByPartnerAsync(Guid partnerId) =>
        await _context.Set<PartnerBlackoutDate>()
            .Where(x => x.PartnerId == partnerId)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync();

    public Task<PartnerBlackoutDate?> GetByIdAsync(Guid id) =>
        _context.Set<PartnerBlackoutDate>().FirstOrDefaultAsync(x => x.Id == id);

    public async Task AddAsync(PartnerBlackoutDate entity)
    {
        await _context.Set<PartnerBlackoutDate>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(PartnerBlackoutDate entity)
    {
        _context.Set<PartnerBlackoutDate>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
