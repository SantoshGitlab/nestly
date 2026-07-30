namespace Nestly.Application.Abstractions.Caching;

/// <summary>
/// Typed distributed-cache abstraction (T017). Application code depends on this
/// rather than on <c>IDistributedCache</c> directly, so the backing store —
/// Redis in deployed environments, in-process memory locally — stays an
/// Infrastructure concern and cached values stay strongly typed.
/// </summary>
/// <remarks>
/// Implementations must treat the cache as advisory: a cache miss, a
/// deserialization failure, or an unreachable cache server must degrade to the
/// underlying source of truth rather than surface as an error. Callers can
/// therefore assume these methods do not throw for transport-level problems.
/// </remarks>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or <c>default</c>
    /// when the key is absent or its payload can no longer be deserialized
    /// (for example after the cached type's shape changed between deploys).
    /// </summary>
    /// <remarks>
    /// When <typeparamref name="T"/> is a non-nullable value type (<c>bool</c>,
    /// <c>int</c>, ...), a miss and a legitimately cached <c>default(T)</c> both
    /// return the same value - there is no way to tell them apart through this
    /// method alone. Code that must tell a miss from a cached default for such
    /// a type should rely on <see cref="GetOrCreateAsync{T}"/>, which resolves
    /// presence from the raw payload rather than from the deserialized value.
    /// </remarks>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/>.
    /// </summary>
    /// <param name="absoluteExpiration">
    /// Time-to-live for the entry. When <c>null</c>, the configured default TTL
    /// applies — entries are never written without an expiry, so a stale value
    /// cannot outlive the process indefinitely.
    /// </param>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes <paramref name="key"/>. Succeeds silently when absent.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, invoking
    /// <paramref name="factory"/> and caching its result on a miss.
    /// </summary>
    /// <remarks>
    /// This is a cache-aside helper, not a lock: concurrent misses for the same
    /// key may each invoke <paramref name="factory"/>. That is deliberate —
    /// distributed locking costs more than the duplicate reads it avoids for
    /// the catalog-shaped data this cache serves. Do not use it where the
    /// factory has side effects.
    /// </remarks>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default);
}
