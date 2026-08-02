namespace Nestly.Application.Subscriptions;

/// <summary>
/// The Hangfire-scheduled recurring billing sweep (PRODUCT-ENHANCEMENTS.md
/// #1, task 178): charges every subscription whose next billing date is due,
/// retrying with backoff on failure before auto-suspending, and dispatches
/// the "expiring soon" reminder (task 183) for subscriptions nearing their
/// next charge. Same shape as <c>IWalletCreditExpirySweepJob</c>/
/// <c>IRecurringBookingSchedulerService</c> - a single idempotent sweep
/// method, registered as one Hangfire recurring job.
/// </summary>
public interface ISubscriptionBillingJob
{
    Task ProcessDueBillingAsync(CancellationToken cancellationToken = default);
}
