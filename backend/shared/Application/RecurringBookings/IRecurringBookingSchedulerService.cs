namespace Nestly.Application.RecurringBookings;

/// <summary>
/// The Hangfire-invoked recurring job (task 185). Called on a daily
/// recurring schedule (see <c>BackgroundJobRegistration</c>) - not enqueued
/// per-plan, since a single sweep over every due plan is simpler to reason
/// about and matches this codebase's existing job style (<c>ExportJobService.ProcessAsync</c>
/// re-reads current state from the database rather than trusting anything
/// captured at enqueue time).
/// </summary>
public interface IRecurringBookingSchedulerService
{
    /// <summary>
    /// For every active plan due within the configured lead time: attempts a
    /// real booking through <c>IBookingService.CreateAsync</c>; on success
    /// advances the plan and notifies the customer of the upcoming visit; on
    /// failure (slot no longer available, or any other orchestration
    /// rejection) skips the occurrence, advances the plan without charging
    /// it against <c>OccurrenceCount</c>, and notifies the customer instead
    /// of failing silently. Safe to re-run (Hangfire's global retry policy
    /// expects idempotent jobs) - already-processed dates are skipped via
    /// <see cref="IRecurringBookingOccurrenceRepository.ExistsForDateAsync"/>.
    /// </summary>
    Task ProcessDueOccurrencesAsync(CancellationToken cancellationToken);
}
