using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ServiceVariantRepository : IServiceVariantRepository
{
    private readonly NestlyDbContext _context;

    public ServiceVariantRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ServiceVariant entity)
    {
        await _context.Set<ServiceVariant>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceVariant entity)
    {
        _context.Set<ServiceVariant>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ServiceVariant entity)
    {
        _context.Set<ServiceVariant>().Remove(entity);
        await _context.SaveChangesAsync();
    }

    public Task<ServiceVariant?> GetByIdAsync(Guid id) =>
        _context.Set<ServiceVariant>().FirstOrDefaultAsync(v => v.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<ServiceVariant>().AnyAsync(v => v.Id == id);

    public async Task<IReadOnlyList<ServiceVariant>> ListByServiceAsync(Guid serviceId) =>
        await _context.Set<ServiceVariant>()
            .Where(v => v.ServiceId == serviceId)
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Name)
            .ToListAsync();

    public async Task<IReadOnlyList<ServiceVariant>> ListActiveByServiceIdsAsync(IReadOnlyCollection<Guid> serviceIds)
    {
        if (serviceIds.Count == 0)
        {
            return [];
        }

        return await _context.Set<ServiceVariant>()
            .AsNoTracking()
            .Where(v => serviceIds.Contains(v.ServiceId) && v.IsActive)
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Name)
            .ToListAsync();
    }
}
