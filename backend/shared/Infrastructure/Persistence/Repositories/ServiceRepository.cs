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

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<Service>().AnyAsync(s => s.Id == id);

    public async Task<IReadOnlyList<Service>> ListActiveByCategoryAsync(Guid categoryId) =>
        await _context.Set<Service>()
            .Where(s => s.CategoryId == categoryId && s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

    public Task<Service?> GetBySlugAsync(string slug) =>
        _context.Set<Service>().FirstOrDefaultAsync(s => s.Slug == slug);

    public Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null) =>
        _context.Set<Service>().AnyAsync(s => s.Slug == slug && (excludeId == null || s.Id != excludeId));

    public async Task<IReadOnlyList<Service>> SearchActiveAsync(string query)
    {
        string normalized = query.ToLowerInvariant();
        return await _context.Set<Service>()
            .Where(s => s.IsActive && s.Name.ToLower().Contains(normalized))
            .OrderBy(s => s.Name)
            .ToListAsync();
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
