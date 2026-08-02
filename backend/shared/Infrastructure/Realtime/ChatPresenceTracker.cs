using Microsoft.Extensions.Logging;
using Nestly.Application.Abstractions.Caching;
using Nestly.Application.Chat;

namespace Nestly.Infrastructure.Realtime;

/// <summary>
/// <see cref="IChatPresenceTracker"/> backed by <see cref="ICacheService"/>
/// (task 190) - see the interface's doc comment for why this must be a
/// shared store, not an in-process dictionary.
/// </summary>
/// <remarks>
/// Reads-then-writes a small connection-id set per user rather than using an
/// atomic Redis command directly: <see cref="ICacheService"/> is the only
/// cache abstraction this codebase exposes to Application-layer-adjacent
/// code, and a rare lost update here (two tabs connecting in the same
/// instant) only ever produces a spurious "online" or "offline" - never a
/// silently dropped notification, per the interface's documented failure
/// mode. Introducing a second, lower-level Redis client just for atomicity
/// on a best-effort signal would be over-engineering for what this needs.
/// </remarks>
public sealed class ChatPresenceTracker : IChatPresenceTracker
{
    private static readonly TimeSpan PresenceTtl = TimeSpan.FromMinutes(15);

    private readonly ICacheService _cache;
    private readonly ILogger<ChatPresenceTracker> _logger;

    public ChatPresenceTracker(ICacheService cache, ILogger<ChatPresenceTracker> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task MarkOnlineAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        string key = CacheKeys.ChatPresence(userId);
        var connections = await _cache.GetAsync<List<string>>(key, cancellationToken) ?? [];
        if (!connections.Contains(connectionId))
        {
            connections.Add(connectionId);
        }

        await _cache.SetAsync(key, connections, PresenceTtl, cancellationToken);
    }

    public async Task MarkOfflineAsync(Guid userId, string connectionId, CancellationToken cancellationToken = default)
    {
        string key = CacheKeys.ChatPresence(userId);
        var connections = await _cache.GetAsync<List<string>>(key, cancellationToken) ?? [];
        connections.Remove(connectionId);

        if (connections.Count == 0)
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        else
        {
            await _cache.SetAsync(key, connections, PresenceTtl, cancellationToken);
        }
    }

    public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var connections = await _cache.GetAsync<List<string>>(CacheKeys.ChatPresence(userId), cancellationToken);
            return connections is { Count: > 0 };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Fail open toward notifying (see interface doc comment): an
            // unreachable presence store must never be mistaken for "online".
            _logger.LogWarning(exception, "Presence lookup failed for user {UserId}; treating as offline.", userId);
            return false;
        }
    }
}
