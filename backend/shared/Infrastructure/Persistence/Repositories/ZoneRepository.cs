using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ZoneRepository : IZoneRepository
{
    private readonly NestlyDbContext _context;

    public ZoneRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Zone entity)
    {
        await _context.Set<Zone>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Zone entity)
    {
        _context.Set<Zone>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<Zone?> GetByIdAsync(Guid id) =>
        _context.Set<Zone>().FirstOrDefaultAsync(z => z.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<Zone>().AnyAsync(z => z.Id == id);

    public async Task<IReadOnlyList<Zone>> ListAsync(Guid? cityId) =>
        await _context.Set<Zone>()
            .Include(z => z.City)
            .Where(z => cityId == null || z.CityId == cityId)
            .OrderBy(z => z.Name)
            .ToListAsync();

    public Task<bool> ExistsByNameInCityAsync(Guid cityId, string name, Guid? excludeId = null) =>
        _context.Set<Zone>().AnyAsync(z =>
            z.CityId == cityId && z.Name == name && (excludeId == null || z.Id != excludeId));
}
