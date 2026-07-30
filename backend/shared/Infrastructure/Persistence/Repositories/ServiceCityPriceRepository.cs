using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ServiceCityPriceRepository : IServiceCityPriceRepository
{
    private readonly NestlyDbContext _context;

    public ServiceCityPriceRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ServiceCityPrice entity)
    {
        await _context.Set<ServiceCityPrice>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceCityPrice entity)
    {
        _context.Set<ServiceCityPrice>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<ServiceCityPrice?> GetByIdAsync(Guid id) =>
        _context.Set<ServiceCityPrice>().FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<ServiceCityPrice>().AnyAsync(p => p.Id == id);

    public Task<ServiceCityPrice?> GetForServiceAndCityAsync(Guid serviceId, Guid cityId) =>
        _context.Set<ServiceCityPrice>().FirstOrDefaultAsync(p => p.ServiceId == serviceId && p.CityId == cityId);
}
