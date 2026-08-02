using Nestly.Domain;

namespace Nestly.Application.RecurringBookings;

public interface IRecurringBookingOccurrenceRepository
{
    Task AddAsync(RecurringBookingOccurrence occurrence);

    /// <summary>The scheduler's idempotency check - see <see cref="RecurringBookingOccurrence"/>'s doc comment.</summary>
    Task<bool> ExistsForDateAsync(Guid recurringBookingPlanId, DateOnly scheduledDate);

    Task<IReadOnlyList<RecurringBookingOccurrence>> ListByPlanAsync(Guid recurringBookingPlanId);
}
