using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ServiceAddOnGroupRepository : IServiceAddOnGroupRepository
{
    private readonly NestlyDbContext _context;

    public ServiceAddOnGroupRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ServiceAddOnGroup entity)
    {
        await _context.Set<ServiceAddOnGroup>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceAddOnGroup entity)
    {
        _context.Set<ServiceAddOnGroup>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ServiceAddOnGroup entity)
    {
        _context.Set<ServiceAddOnGroup>().Remove(entity);
        await _context.SaveChangesAsync();
    }

    public Task<ServiceAddOnGroup?> GetByIdAsync(Guid id) =>
        _context.Set<ServiceAddOnGroup>().FirstOrDefaultAsync(g => g.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<ServiceAddOnGroup>().AnyAsync(g => g.Id == id);

    public async Task<IReadOnlyList<ServiceAddOnGroup>> ListAllAsync(Guid? serviceId)
    {
        IQueryable<ServiceAddOnGroup> query = _context.Set<ServiceAddOnGroup>();
        if (serviceId is not null)
        {
            query = query.Where(g => g.ServiceId == serviceId);
        }

        return await query.OrderBy(g => g.SortOrder).ThenBy(g => g.Name).ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceAddOnGroup>> ListByServiceIdsAsync(IReadOnlyCollection<Guid> serviceIds)
    {
        if (serviceIds.Count == 0)
        {
            return [];
        }

        return await _context.Set<ServiceAddOnGroup>()
            .AsNoTracking()
            .Where(g => serviceIds.Contains(g.ServiceId))
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<Guid, ServiceAddOnGroup>> GetByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, ServiceAddOnGroup>();
        }

        return await _context.Set<ServiceAddOnGroup>()
            .AsNoTracking()
            .Where(g => ids.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id);
    }
}
