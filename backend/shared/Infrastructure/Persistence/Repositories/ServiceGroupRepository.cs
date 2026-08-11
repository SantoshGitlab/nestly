using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ServiceGroupRepository : IServiceGroupRepository
{
    private readonly NestlyDbContext _context;

    public ServiceGroupRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ServiceGroup entity)
    {
        await _context.Set<ServiceGroup>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceGroup entity)
    {
        _context.Set<ServiceGroup>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ServiceGroup entity)
    {
        _context.Set<ServiceGroup>().Remove(entity);
        await _context.SaveChangesAsync();
    }

    public Task<ServiceGroup?> GetByIdAsync(Guid id) =>
        _context.Set<ServiceGroup>().FirstOrDefaultAsync(g => g.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<ServiceGroup>().AnyAsync(g => g.Id == id);

    public async Task<IReadOnlyList<ServiceGroup>> ListAllAsync(Guid? categoryId)
    {
        IQueryable<ServiceGroup> query = _context.Set<ServiceGroup>();
        if (categoryId is not null)
        {
            query = query.Where(g => g.CategoryId == categoryId);
        }

        return await query.OrderBy(g => g.SortOrder).ThenBy(g => g.Name).ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceGroup>> ListActiveByCategoryIdsAsync(IReadOnlyCollection<Guid> categoryIds)
    {
        if (categoryIds.Count == 0)
        {
            return [];
        }

        return await _context.Set<ServiceGroup>()
            .AsNoTracking()
            .Where(g => categoryIds.Contains(g.CategoryId) && g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Name)
            .ToListAsync();
    }
}
