using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ServiceAddOnRepository : IServiceAddOnRepository
{
    private readonly NestlyDbContext _context;

    public ServiceAddOnRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ServiceAddOn entity)
    {
        await _context.Set<ServiceAddOn>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceAddOn entity)
    {
        _context.Set<ServiceAddOn>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<ServiceAddOn?> GetByIdAsync(Guid id) =>
        _context.Set<ServiceAddOn>().FirstOrDefaultAsync(a => a.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<ServiceAddOn>().AnyAsync(a => a.Id == id);

    public async Task<IReadOnlyList<ServiceAddOn>> ListActiveByServiceAsync(Guid serviceId) =>
        await _context.Set<ServiceAddOn>()
            .Where(a => a.ServiceId == serviceId && a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToListAsync();
}
