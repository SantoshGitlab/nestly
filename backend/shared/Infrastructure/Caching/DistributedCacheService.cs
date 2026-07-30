using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.Abstractions.Caching;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Caching;

/// <summary>
/// <see cref="ICacheService"/> backed by <see cref="IDistributedCache"/> (T017).
/// Resolves to Redis when configured and to an in-process store otherwise; this
/// class is unaware of which, so behaviour is identical in tests and production.
/// </summary>
public sealed class DistributedCacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly CacheOptions _options;
    private readonly ILogger<DistributedCacheService> _logger;

    public DistributedCacheService(
        IDistributedCache cache,
        IOptions<CacheOptions> options,
        ILogger<DistributedCacheService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        byte[]? payload;
        try
        {
            payload = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The cache is advisory: an unreachable server must not fail the
            // request. Callers fall through to the source of truth.
            _logger.LogWarning(exception, "Cache read failed for key {CacheKey}; treating as a miss.", key);
            return default;
        }

        if (payload is null or { Length: 0 })
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(payload, SerializerOptions);
        }
        catch (JsonException exception)
        {
            // A payload written by an older shape of T. Evict it so the entry
            // repairs itself rather than failing every read until it expires.
            _logger.LogWarning(
                exception,
                "Cached payload for key {CacheKey} could not be deserialized as {CachedType}; evicting.",
                key,
                typeof(T).Name);

            await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (value is null)
        {
            // Caching null would make a miss indistinguishable from a cached
            // absence on read; callers should evict instead.
            await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return;
        }

        var entryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration ?? _options.DefaultExpiration,
        };

        try
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
            await _cache.SetAsync(key, payload, entryOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Never surface a cache write failure to the caller: the write it
            // accompanies has already succeeded against the source of truth.
            _logger.LogWarning(exception, "Cache write failed for key {CacheKey}.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Worth a warning rather than silence: a failed eviction means
            // readers keep seeing a stale value until its TTL elapses.
            _logger.LogWarning(exception, "Cache eviction failed for key {CacheKey}.", key);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        // Deliberately does not delegate to GetAsync<T>: that method signals a
        // miss by returning default(T), which for a non-nullable value type
        // (bool, int, ...) is bitwise identical to a legitimately cached
        // default value. Checking the raw payload's presence here instead
        // keeps hit/miss detection unambiguous for every T.
        byte[]? payload;
        try
        {
            payload = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Cache read failed for key {CacheKey}; treating as a miss.", key);
            payload = null;
        }

        if (payload is { Length: > 0 })
        {
            try
            {
                return JsonSerializer.Deserialize<T>(payload, SerializerOptions)!;
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Cached payload for key {CacheKey} could not be deserialized as {CachedType}; evicting.",
                    key,
                    typeof(T).Name);

                await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }
        }

        T created = await factory(cancellationToken).ConfigureAwait(false);
        await SetAsync(key, created, absoluteExpiration, cancellationToken).ConfigureAwait(false);

        return created;
    }
}
