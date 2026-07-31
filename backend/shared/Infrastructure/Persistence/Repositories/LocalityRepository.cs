using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class LocalityRepository : ILocalityRepository
{
    private readonly NestlyDbContext _context;

    public LocalityRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Locality entity)
    {
        await _context.Set<Locality>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Locality entity)
    {
        _context.Set<Locality>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<Locality?> GetByIdAsync(Guid id) =>
        _context.Set<Locality>().FirstOrDefaultAsync(l => l.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<Locality>().AnyAsync(l => l.Id == id);

    public async Task<IReadOnlyList<Locality>> ListAsync(Guid? zoneId) =>
        await _context.Set<Locality>()
            .Include(l => l.Zone)
            .Include(l => l.Pincode)
            .Where(l => zoneId == null || l.ZoneId == zoneId)
            .OrderBy(l => l.Name)
            .ToListAsync();

    public Task<bool> ExistsByNameInZoneAsync(Guid zoneId, string name, Guid? excludeId = null) =>
        _context.Set<Locality>().AnyAsync(l =>
            l.ZoneId == zoneId && l.Name == name && (excludeId == null || l.Id != excludeId));
}
