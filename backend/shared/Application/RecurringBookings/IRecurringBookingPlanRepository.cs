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

    /// <summary>
    /// The cadence of each of these plans, in one round trip (task 300).
    /// Plan ids with no matching row are simply absent from the result.
    ///
    /// Deliberately a projection rather than <see cref="GetByIdAsync"/> per
    /// id: the provider job list resolves a plan per row, and the only field
    /// it needs is the frequency. It reads that live through
    /// <see cref="Domain.Booking.RecurringBookingPlanId"/> rather than off a
    /// column snapshotted onto the booking, so a customer who switches a plan
    /// from weekly to monthly does not leave every already-generated job
    /// advertising the old cadence - the same reasoning
    /// <see cref="RecurringBookingPlan"/>'s class doc comment gives for
    /// holding live address/slot-window references instead of snapshots.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, RecurringBookingRecurrenceFrequency>> ListFrequenciesByIdsAsync(
        IReadOnlyCollection<Guid> planIds);
}
