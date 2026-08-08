namespace Nestly.Application.Notifications;

/// <summary>
/// Task 294's retry path: the scheduled sweep that delivers notification
/// intents the in-process handler never got to. Registered as a Hangfire
/// recurring job the same way <c>IBookingExpirySweepJob</c> is - the interface
/// lives in Application so Hangfire's activator can resolve it through DI.
/// </summary>
public interface INotificationIntentSweepJob
{
    /// <summary>
    /// Idempotent and safe to re-run, and safe to run concurrently on several
    /// app instances: every intent is taken by a conditional UPDATE, so at
    /// most one sweep can be sending any given message at any time, and an
    /// intent the in-process path already delivered is never selected at all.
    /// </summary>
    /// <returns>How many intents were dispatched by this pass - zero on a healthy system, which is the point.</returns>
    Task<int> SweepAsync(CancellationToken cancellationToken = default);
}
