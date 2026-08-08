using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain;

namespace Nestly.Application.Notifications;

/// <summary>
/// The gate every notification send now passes through (task 294). Wrapped
/// around a dispatch, it makes that dispatch happen at most once across the
/// in-process fast path and every subsequent sweep, on every app instance.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fails open, always.</b> If no intent row exists for a key - the planner
/// did not foresee this message, or the interceptor is not wired up in some
/// host - the send still happens and a warning is logged. The intent mechanism
/// exists to stop notifications being lost; it must never become a new way to
/// lose one. The cost of failing open is that such a message is back to
/// at-most-once, which is where the whole system was before this existed.
/// </para>
/// <para>
/// <b>Scoped, and it remembers.</b> The sweep claims a row itself
/// (<see cref="TryClaimForSweepAsync"/>) and then re-invokes the ordinary
/// handler, which will try to claim the same row again a moment later. That
/// second claim must succeed - it is the same worker - so the coordinator
/// tracks the keys its own scope already holds. This is why it is registered
/// scoped and why the sweep processes an intent inside the scope it claimed
/// it in.
/// </para>
/// </remarks>
public interface INotificationIntentCoordinator
{
    /// <summary>
    /// Runs <paramref name="send"/> exactly once for
    /// (<paramref name="domainEvent"/>, <paramref name="eventType"/>) across
    /// all delivery paths, then marks the intent delivered. Does nothing if
    /// another path already holds or completed it.
    /// </summary>
    /// <remarks>
    /// A throw from <paramref name="send"/> is recorded against the intent and
    /// then propagates, preserving the pre-existing contract that a failing
    /// post-commit handler surfaces its failure to the caller. The difference
    /// is that the message is no longer lost when it does: the row stays
    /// pending and the sweep picks it up.
    /// </remarks>
    Task DeliverAsync(
        IDomainEvent domainEvent,
        NotificationEventType eventType,
        Func<CancellationToken, Task> send,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves an intent that is deliberately not being sent - muted by ops,
    /// recipient on a live connection, subject row deleted. Without this the
    /// row would sit pending and be swept until it was abandoned, turning
    /// every deliberate silence into a false alarm.
    /// </summary>
    Task SkipAsync(
        IDomainEvent domainEvent,
        NotificationEventType eventType,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sweep-side claim. Takes the lease and remembers it for this scope so
    /// the handler's own <see cref="DeliverAsync"/> on the same key proceeds
    /// instead of deadlocking against the sweep's own lease. Returns false
    /// when another instance won the row.
    /// </summary>
    Task<bool> TryClaimForSweepAsync(NotificationIntent intent, CancellationToken cancellationToken = default);
}
