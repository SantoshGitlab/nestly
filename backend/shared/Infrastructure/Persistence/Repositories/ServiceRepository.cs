using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ServiceRepository : IServiceRepository
{
    private readonly NestlyDbContext _context;

    public ServiceRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Service entity)
    {
        await _context.Set<Service>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Service entity)
    {
        _context.Set<Service>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<Service?> GetByIdAsync(Guid id) =>
        _context.Set<Service>().FirstOrDefaultAsync(s => s.Id == id);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _context.Set<Service>()
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name);
    }


    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<Service>().AnyAsync(s => s.Id == id);

    // AsNoTracking (task 136a): both queries below back read-only catalog
    // browse/search paths (ServiceQueryService/CategoryQueryService's cached
    // listings, CatalogSearchService's search) - nothing downstream mutates
    // and saves these entities.

    public async Task<IReadOnlyList<Service>> ListActiveByCategoryAsync(Guid categoryId) =>
        await _context.Set<Service>()
            .AsNoTracking()
            .Where(s => s.CategoryId == categoryId && s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

    public Task<Service?> GetBySlugAsync(string slug) =>
        _context.Set<Service>().FirstOrDefaultAsync(s => s.Slug == slug);

    public Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null) =>
        _context.Set<Service>().AnyAsync(s => s.Slug == slug && (excludeId == null || s.Id != excludeId));

    public async Task<IReadOnlyList<Service>> SearchActiveAsync(string query, int? limit = null)
    {
        string normalized = query.ToLowerInvariant();
        var results = _context.Set<Service>()
            .AsNoTracking()
            .Where(s => s.IsActive && s.Name.ToLower().Contains(normalized))
            .OrderBy(s => s.Name)
            .AsQueryable();

        if (limit is not null)
        {
            results = results.Take(limit.Value);
        }

        return await results.ToListAsync();
    }

    public async Task<IReadOnlyList<Service>> ListAllAsync(Guid? categoryId)
    {
        IQueryable<Service> query = _context.Set<Service>();
        if (categoryId is not null)
        {
            query = query.Where(s => s.CategoryId == categoryId);
        }

        return await query.OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync();
    }

    public async Task<IReadOnlyList<Service>> ListAllAsync() =>
        await _context.Set<Service>().OrderBy(s => s.Name).ToListAsync();
}
