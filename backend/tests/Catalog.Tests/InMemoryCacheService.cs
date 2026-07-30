using Nestly.Application.Abstractions.Caching;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Real (non-mocked) <see cref="ICacheService"/> backed by an in-process
/// dictionary, so caching/invalidation tests exercise actual get/set/remove
/// semantics rather than a stubbed no-op. TTL is intentionally not enforced -
/// no test in this suite waits out an expiration, so tracking it would only
/// add untested behaviour of its own.
/// </summary>
public sealed class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, object> _store = [];

    public IReadOnlyCollection<string> Keys => _store.Keys;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.TryGetValue(key, out var value) ? (T?)value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            _store.Remove(key);
        }
        else
        {
            _store[key] = value;
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        // Checks dictionary presence directly rather than going through
        // GetAsync<T> + a null-check: for a non-nullable value type T (bool,
        // int, ...) an unconstrained T? degrades to plain T at runtime, so a
        // genuine miss and a legitimately cached default(T) would otherwise be
        // indistinguishable. Mirrors DistributedCacheService's fix for the
        // same issue.
        if (_store.TryGetValue(key, out var existing))
        {
            return (T)existing;
        }

        var created = await factory(cancellationToken);
        await SetAsync(key, created, absoluteExpiration, cancellationToken);
        return created;
    }
}
