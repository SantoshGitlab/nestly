using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class SlotAvailabilityOverrideRepository : ISlotAvailabilityOverrideRepository
{
    private readonly NestlyDbContext _context;

    public SlotAvailabilityOverrideRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SlotAvailabilityOverride entity)
    {
        await _context.Set<SlotAvailabilityOverride>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SlotAvailabilityOverride entity)
    {
        _context.Set<SlotAvailabilityOverride>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<SlotAvailabilityOverride?> GetByIdAsync(Guid id) =>
        _context.Set<SlotAvailabilityOverride>().FirstOrDefaultAsync(o => o.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<SlotAvailabilityOverride>().AnyAsync(o => o.Id == id);

    public async Task<IReadOnlyList<SlotAvailabilityOverride>> ListAsync(Guid? cityId, DateOnly? date) =>
        await _context.Set<SlotAvailabilityOverride>()
            .Where(o => (cityId == null || o.CityId == cityId) && (date == null || o.Date == date))
            .OrderByDescending(o => o.Date)
            .ToListAsync();

    public async Task DeleteAsync(SlotAvailabilityOverride entity)
    {
        _context.Set<SlotAvailabilityOverride>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
