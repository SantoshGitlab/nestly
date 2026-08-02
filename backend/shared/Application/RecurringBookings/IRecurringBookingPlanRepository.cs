using Nestly.Domain;

namespace Nestly.Application.RecurringBookings;

public interface IRecurringBookingPlanRepository
{
    Task AddAsync(RecurringBookingPlan plan);

    Task UpdateAsync(RecurringBookingPlan plan);

    /// <summary>Loaded with its add-ons - a plan is never useful partially loaded.</summary>
    Task<RecurringBookingPlan?> GetByIdAsync(Guid id);

    Task<IReadOnlyList<RecurringBookingPlan>> ListByCustomerAsync(Guid customerId);

    /// <summary>
    /// Active plans whose <see cref="RecurringBookingPlan.NextOccurrenceDate"/>
    /// falls on or before <paramref name="onOrBefore"/> - the scheduler's due
    /// set (task 185). <paramref name="onOrBefore"/> is "today plus lead
    /// time", not just "today", so an occurrence is attempted with enough
    /// runway for a skip-and-notify to reach the customer before the visit
    /// was due.
    /// </summary>
    Task<IReadOnlyList<RecurringBookingPlan>> ListDueAsync(DateOnly onOrBefore);
}
