using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly NestlyDbContext _context;

    public CategoryRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Category entity)
    {
        await _context.Set<Category>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Category entity)
    {
        _context.Set<Category>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<Category?> GetByIdAsync(Guid id) =>
        _context.Set<Category>().FirstOrDefaultAsync(c => c.Id == id);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _context.Set<Category>()
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name);
    }


    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<Category>().AnyAsync(c => c.Id == id);

    public Task<Category?> GetBySlugAsync(string slug) =>
        _context.Set<Category>().FirstOrDefaultAsync(c => c.Slug == slug);

    public Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null) =>
        _context.Set<Category>().AnyAsync(c => c.Slug == slug && (excludeId == null || c.Id != excludeId));

    public async Task<IReadOnlyList<Category>> ListAllAsync() =>
        await _context.Set<Category>()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

    // AsNoTracking (task 136a): both queries below are read-only call paths
    // (CategoryQueryService's cached city listing, CatalogSearchService's
    // search) - nothing downstream ever mutates and saves these entities, so
    // there is no reason to pay for EF Core's change-tracking snapshot on a
    // possibly-large result set.

    public async Task<IReadOnlyList<Category>> ListServiceableInCityAsync(Guid cityId)
    {
        var categoryIds = _context.Set<CategoryCityMapping>()
            .Where(m => m.CityId == cityId && m.IsActive)
            .Select(m => m.CategoryId);

        return await _context.Set<Category>()
            .AsNoTracking()
            .Where(c => c.IsActive && categoryIds.Contains(c.Id))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Category>> SearchActiveAsync(string query, int? limit = null)
    {
        string normalized = query.ToLowerInvariant();
        var results = _context.Set<Category>()
            .AsNoTracking()
            .Where(c => c.IsActive && c.Name.ToLower().Contains(normalized))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .AsQueryable();

        if (limit is not null)
        {
            results = results.Take(limit.Value);
        }

        return await results.ToListAsync();
    }

    public async Task<IReadOnlyList<Category>> ListChildrenAsync(Guid parentCategoryId) =>
        await _context.Set<Category>()
            .AsNoTracking()
            .Where(c => c.ParentCategoryId == parentCategoryId && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
}
