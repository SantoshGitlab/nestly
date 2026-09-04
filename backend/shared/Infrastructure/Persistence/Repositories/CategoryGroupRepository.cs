using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CategoryGroupRepository : ICategoryGroupRepository
{
    private readonly NestlyDbContext _context;

    public CategoryGroupRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CategoryGroup entity)
    {
        await _context.Set<CategoryGroup>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CategoryGroup entity)
    {
        _context.Set<CategoryGroup>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(CategoryGroup entity)
    {
        _context.Set<CategoryGroup>().Remove(entity);
        await _context.SaveChangesAsync();
    }

    public Task<CategoryGroup?> GetByIdAsync(Guid id) =>
        _context.Set<CategoryGroup>().FirstOrDefaultAsync(g => g.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<CategoryGroup>().AnyAsync(g => g.Id == id);

    public async Task<IReadOnlyList<CategoryGroup>> ListAllAsync(Guid? categoryId)
    {
        IQueryable<CategoryGroup> query = _context.Set<CategoryGroup>();
        if (categoryId is not null)
        {
            query = query.Where(g => g.CategoryId == categoryId);
        }

        return await query.OrderBy(g => g.SortOrder).ThenBy(g => g.Name).ToListAsync();
    }

    public async Task<IReadOnlyList<CategoryGroup>> ListActiveByCategoryIdsAsync(IReadOnlyCollection<Guid> categoryIds)
    {
        if (categoryIds.Count == 0)
        {
            return [];
        }

        return await _context.Set<CategoryGroup>()
            .AsNoTracking()
            .Where(g => categoryIds.Contains(g.CategoryId) && g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Name)
            .ToListAsync();
    }
}
