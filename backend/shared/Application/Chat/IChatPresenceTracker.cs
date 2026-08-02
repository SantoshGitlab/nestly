namespace Nestly.Application.Chat;

/// <summary>
/// Tracks which users currently hold a live SignalR connection (task 190),
/// backed by the same shared Redis instance every API already uses for
/// caching (see <c>ICacheService</c>) - consumer-api and admin-api are
/// separate processes, so an in-memory dictionary in one could never answer
/// "is the customer connected" for a message an admin just sent from the
/// other. This is a best-effort presence signal for task 194's offline-push
/// decision, not a source of truth for anything correctness-critical: a
/// missed "offline" transition just means one redundant notification is
/// sent to someone who was actually online, and a missed "online" (or a
/// tracker read failure) must resolve to "treat as offline" so the fallback
/// notification still fires - silently skipping a real notification is the
/// one failure mode this must never produce.
/// </summary>
public interface IChatPresenceTracker
{
    Task MarkOnlineAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);

    Task MarkOfflineAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default);

    Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken = default);
}
