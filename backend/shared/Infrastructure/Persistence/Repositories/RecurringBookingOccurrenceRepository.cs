using Microsoft.EntityFrameworkCore;
using Nestly.Application.RecurringBookings;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Repositories;

public class RecurringBookingOccurrenceRepository : IRecurringBookingOccurrenceRepository
{
    private readonly NestlyDbContext _context;

    public RecurringBookingOccurrenceRepository(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RecurringBookingOccurrence occurrence)
    {
        await _context.RecurringBookingOccurrences.AddAsync(occurrence);
        await _context.SaveChangesAsync();
    }

    public Task<bool> ExistsForDateAsync(Guid recurringBookingPlanId, DateOnly scheduledDate) =>
        _context.RecurringBookingOccurrences
            .AnyAsync(o => o.RecurringBookingPlanId == recurringBookingPlanId && o.ScheduledDate == scheduledDate);

    public async Task<IReadOnlyList<RecurringBookingOccurrence>> ListByPlanAsync(Guid recurringBookingPlanId) =>
        await _context.RecurringBookingOccurrences
            .AsNoTracking()
            .Where(o => o.RecurringBookingPlanId == recurringBookingPlanId)
            .OrderByDescending(o => o.ScheduledDate)
            .ToListAsync();
}
