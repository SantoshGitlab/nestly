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
}
