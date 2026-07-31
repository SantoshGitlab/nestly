using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class SlotWindowRepository : ISlotWindowRepository
{
    private readonly NestlyDbContext _context;

    public SlotWindowRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SlotWindow entity)
    {
        await _context.Set<SlotWindow>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SlotWindow entity)
    {
        _context.Set<SlotWindow>().Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task<SlotWindow?> GetByIdAsync(Guid id) =>
        _context.Set<SlotWindow>().FirstOrDefaultAsync(w => w.Id == id);

    public Task<bool> ExistsAsync(Guid id) =>
        _context.Set<SlotWindow>().AnyAsync(w => w.Id == id);

    public async Task<IReadOnlyList<SlotWindow>> ListActiveForCityAndDayAsync(Guid cityId, DayOfWeek dayOfWeek)
    {
        var windowIds = _context.Set<SlotWindowRule>()
            .Where(r => r.DayOfWeek == dayOfWeek)
            .Select(r => r.SlotWindowId);

        var windows = await _context.Set<SlotWindow>()
            .Where(w => w.CityId == cityId && w.IsActive && windowIds.Contains(w.Id))
            .ToListAsync();

        // Ordered client-side: SQLite (test provider) can't ORDER BY an interval/TimeSpan column.
        return windows.OrderBy(w => w.StartTime).ToList();
    }

    public async Task<IReadOnlyList<SlotWindow>> ListAsync(Guid? cityId)
    {
        var windows = await _context.Set<SlotWindow>()
            .Include(w => w.City)
            .Where(w => cityId == null || w.CityId == cityId)
            .ToListAsync();

        // Ordered client-side: SQLite (test provider) can't ORDER BY an interval/TimeSpan column.
        return windows.OrderBy(w => w.City!.Name).ThenBy(w => w.StartTime).ToList();
    }

    public async Task<IReadOnlyList<DayOfWeek>> ListRuleDaysAsync(Guid slotWindowId) =>
        await _context.Set<SlotWindowRule>()
            .Where(r => r.SlotWindowId == slotWindowId)
            .Select(r => r.DayOfWeek)
            .ToListAsync();

    public async Task ReplaceRulesAsync(Guid slotWindowId, IReadOnlyList<DayOfWeek> daysOfWeek)
    {
        var existingRules = await _context.Set<SlotWindowRule>()
            .Where(r => r.SlotWindowId == slotWindowId)
            .ToListAsync();
        _context.Set<SlotWindowRule>().RemoveRange(existingRules);

        foreach (var dayOfWeek in daysOfWeek.Distinct())
        {
            await _context.Set<SlotWindowRule>().AddAsync(new SlotWindowRule(Guid.NewGuid(), slotWindowId, dayOfWeek));
        }

        await _context.SaveChangesAsync();
    }
}
