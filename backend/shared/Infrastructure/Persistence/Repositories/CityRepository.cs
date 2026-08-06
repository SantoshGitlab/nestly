using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CityRepository : ICityRepository
{
    private readonly NestlyDbContext _context;

    public CityRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(City entity)
    {
        await _context.Set<City>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(City entity)
    {
        _context.Set<City>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<City?> GetByIdAsync(Guid id) =>
        _context.Set<City>().FirstOrDefaultAsync(c => c.Id == id);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _context.Set<City>()
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name);
    }


    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<City>().AnyAsync(c => c.Id == id);

    public async Task<IReadOnlyList<City>> ListAsync(Guid? stateId) =>
        await _context.Set<City>()
            .Include(c => c.State)
            .Where(c => stateId == null || c.StateId == stateId)
            .OrderBy(c => c.Name)
            .ToListAsync();

    public Task<bool> ExistsByNameInStateAsync(Guid stateId, string name, Guid? excludeId = null) =>
        _context.Set<City>().AnyAsync(c =>
            c.StateId == stateId && c.Name == name && (excludeId == null || c.Id != excludeId));
}
