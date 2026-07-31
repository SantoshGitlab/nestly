using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Serviceability;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class ServicePincodeMappingRepository : IServicePincodeMappingRepository
{
    private readonly NestlyDbContext _context;

    public ServicePincodeMappingRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ServicePincodeMapping entity)
    {
        await _context.Set<ServicePincodeMapping>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServicePincodeMapping entity)
    {
        _context.Set<ServicePincodeMapping>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<ServicePincodeMapping?> GetByIdAsync(Guid id) =>
        _context.Set<ServicePincodeMapping>().FirstOrDefaultAsync(m => m.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<ServicePincodeMapping>().AnyAsync(m => m.Id == id);

    public Task<ServicePincodeMapping?> FindAsync(Guid serviceId, Guid pincodeId) =>
        _context.Set<ServicePincodeMapping>()
            .FirstOrDefaultAsync(m => m.ServiceId == serviceId && m.PincodeId == pincodeId);

    public async Task<IReadOnlyList<ServicePincodeMappingResponse>> ListAsync(Guid? serviceId, Guid? pincodeId) =>
        await (
            from mapping in _context.Set<ServicePincodeMapping>()
            join service in _context.Set<Service>() on mapping.ServiceId equals service.Id
            join pincode in _context.Set<Pincode>() on mapping.PincodeId equals pincode.Id
            where (serviceId == null || mapping.ServiceId == serviceId) &&
                  (pincodeId == null || mapping.PincodeId == pincodeId)
            orderby pincode.Code, service.Name
            select new ServicePincodeMappingResponse(
                mapping.Id, service.Id, service.Name, pincode.Id, pincode.Code, mapping.IsActive)
        ).ToListAsync();
}
