using Nestly.Application.Abstractions.Caching;

namespace Nestly.Performance.Tests;

/// <summary>
/// Real (non-mocked) <see cref="ICacheService"/> backed by an in-process
/// dictionary - a copy of Catalog.Tests/InMemoryCacheService.cs (test
/// doubles are duplicated per test project in this repo rather than shared,
/// matching each project's own TestDatabase.cs).
///
/// Not thread-safe, deliberately: every concurrent caller in this project's
/// tests constructs its own short-lived instance (see each test's
/// per-request service-building helper) rather than sharing one across
/// Task.WhenAll callers, so simulated concurrent customers always hit the
/// database - the realistic worst case for a load test where nothing is
/// warm yet - instead of racing on a shared dictionary that has nothing to
/// do with the behaviour under test.
/// </summary>
public sealed class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, object> _store = [];

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
        if (_store.TryGetValue(key, out var existing))
        {
            return (T)existing;
        }

        var created = await factory(cancellationToken);
        await SetAsync(key, created, absoluteExpiration, cancellationToken);
        return created;
    }
}
