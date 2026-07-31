using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class PromotionalPriceRepository : IPromotionalPriceRepository
{
    private readonly NestlyDbContext _context;

    public PromotionalPriceRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PromotionalPrice entity)
    {
        await _context.Set<PromotionalPrice>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PromotionalPrice entity)
    {
        _context.Set<PromotionalPrice>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<PromotionalPrice?> GetByIdAsync(Guid id) =>
        _context.Set<PromotionalPrice>().FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<PromotionalPrice>().AnyAsync(p => p.Id == id);

    public async Task<IReadOnlyList<PromotionalPrice>> ListAsync(Guid? serviceId) =>
        await _context.Set<PromotionalPrice>()
            .Where(p => serviceId == null || p.ServiceId == serviceId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
}
