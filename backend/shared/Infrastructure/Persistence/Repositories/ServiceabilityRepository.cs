using Microsoft.EntityFrameworkCore;
using Nestly.Application.Serviceability;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ServiceabilityRepository : IServiceabilityRepository
{
    private readonly NestlyDbContext _context;

    public ServiceabilityRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public Task<bool> CityExistsAsync(Guid cityId) =>
        _context.Set<City>().AnyAsync(c => c.Id == cityId);

    public Task<bool> PincodeExistsAsync(Guid pincodeId) =>
        _context.Set<Pincode>().AnyAsync(p => p.Id == pincodeId);

    public async Task<Guid?> GetPincodeIdForLocalityAsync(Guid localityId)
    {
        var locality = await _context.Set<Locality>()
            .Where(l => l.Id == localityId)
            .Select(l => new { l.PincodeId })
            .FirstOrDefaultAsync();

        return locality?.PincodeId;
    }

    public Task<bool> IsCategoryActiveInCityAsync(Guid categoryId, Guid cityId) =>
        _context.Set<CategoryCityMapping>()
            .AnyAsync(m => m.CategoryId == categoryId && m.CityId == cityId && m.IsActive);

    public Task<bool> IsServiceActiveInPincodeAsync(Guid serviceId, Guid pincodeId) =>
        _context.Set<ServicePincodeMapping>()
            .AnyAsync(m => m.ServiceId == serviceId && m.PincodeId == pincodeId && m.IsActive);
}
