using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class SlotBlackoutRepository : ISlotBlackoutRepository
{
    private readonly NestlyDbContext _context;

    public SlotBlackoutRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SlotBlackout entity)
    {
        await _context.Set<SlotBlackout>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SlotBlackout entity)
    {
        _context.Set<SlotBlackout>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<SlotBlackout?> GetByIdAsync(Guid id) =>
        _context.Set<SlotBlackout>().FirstOrDefaultAsync(b => b.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<SlotBlackout>().AnyAsync(b => b.Id == id);

    public async Task<IReadOnlyList<SlotBlackout>> ListInRangeAsync(Guid cityId, DateOnly from, DateOnly to) =>
        await _context.Set<SlotBlackout>()
            .Where(b => b.CityId == cityId && b.StartDate <= to && b.EndDate >= from)
            .ToListAsync();

    public async Task<IReadOnlyList<SlotBlackout>> ListAsync(Guid? cityId) =>
        await _context.Set<SlotBlackout>()
            .Where(b => cityId == null || b.CityId == cityId)
            .OrderByDescending(b => b.StartDate)
            .ToListAsync();

    public async Task DeleteAsync(SlotBlackout entity)
    {
        _context.Set<SlotBlackout>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
