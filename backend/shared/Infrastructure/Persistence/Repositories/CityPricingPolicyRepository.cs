using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class CityPricingPolicyRepository : ICityPricingPolicyRepository
{
    private readonly NestlyDbContext _context;

    public CityPricingPolicyRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CityPricingPolicy entity)
    {
        await _context.Set<CityPricingPolicy>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CityPricingPolicy entity)
    {
        _context.Set<CityPricingPolicy>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<CityPricingPolicy?> GetByIdAsync(Guid id) =>
        _context.Set<CityPricingPolicy>().FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<CityPricingPolicy>().AnyAsync(p => p.Id == id);

    public Task<CityPricingPolicy?> GetByCityAsync(Guid cityId) =>
        _context.Set<CityPricingPolicy>().FirstOrDefaultAsync(p => p.CityId == cityId);

    public async Task<IReadOnlyList<CityPricingPolicy>> ListAsync() =>
        await _context.Set<CityPricingPolicy>().ToListAsync();
}
