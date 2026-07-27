using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "Cache" configuration section (T017).
/// </summary>
public class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Redis connection string. When empty, the application falls back to an
    /// in-process distributed cache — acceptable for local development and
    /// tests, but not for a horizontally scaled deployment, where each replica
    /// would then hold its own divergent copy.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Key prefix isolating this application's entries within a shared Redis
    /// instance. Applied by the Redis cache implementation itself, on top of
    /// the vocabulary in <c>CacheKeys</c>.
    /// </summary>
    public string InstanceName { get; set; } = "nestly:";

    /// <summary>
    /// Default time-to-live applied when a caller does not specify one. Bounded
    /// so a cached projection cannot outlive its source of truth indefinitely.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "24:00:00")]
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// True when a real Redis endpoint is configured.
    /// </summary>
    public bool IsRedisConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}
