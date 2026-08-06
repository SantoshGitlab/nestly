using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "GoogleMaps" configuration section (task
/// 266) - the server-side Google Maps Platform credential and the cost/
/// latency guard rails around it.
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>class, not a record, deliberately</b>: a record's synthesized
/// <c>ToString</c> prints every property, so one careless
/// <c>_logger.LogDebug("{Options}", options)</c> would put
/// <see cref="ApiKey"/> in the log stream. The default
/// <see cref="object.ToString"/> prints only the type name.
/// </para>
/// <para>
/// <see cref="ApiKey"/> is a secret and must come from a secret store or
/// environment variable (<c>GoogleMaps__ApiKey</c>), never from a committed
/// <c>appsettings.json</c> - see DEVOPS.md CONFIGURATION AND SECRETS. It is a
/// <b>server-side</b> key and must be restricted by caller IP and to the
/// Routes API only. It must not be the same key as the browser's
/// <c>NEXT_PUBLIC_GOOGLE_MAPS_API_KEY</c> (task 280), which is public by
/// construction and can only be protected by an HTTP-referrer restriction -
/// a restriction that does nothing for a server call.
/// </para>
/// </remarks>
public class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    /// <summary>
    /// Google's cap on <c>origins x destinations</c> in a single
    /// <c>computeRouteMatrix</c> request. 625 is allowed for
    /// <c>TRAFFIC_UNAWARE</c>, but 100 is the limit under
    /// <c>TRAFFIC_AWARE_OPTIMAL</c> and <c>TRANSIT</c>; pinning to the
    /// tightest of them means changing the routing preference later can never
    /// silently start producing over-limit requests.
    /// </summary>
    public const int MaxElementsPerRequestLimit = 100;

    /// <summary>
    /// Google Maps Platform API key. When absent, the whole integration falls
    /// back to the sandbox estimator, so the system runs with no billing
    /// account configured at all.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Kill switch. Default true, same convention as
    /// <see cref="AutoAssignmentOptions.Enabled"/>: on by default, an explicit
    /// override to turn off. Setting it false forces the sandbox estimator
    /// even when a key is present - the lever ops pulls during a billing
    /// incident or a Google outage, without a deploy and without deleting the
    /// key from the secret store.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Per-request HTTP timeout. Five seconds because this call sits on the
    /// auto-assignment path (task 267) and the location-ping path (task 271);
    /// a slow router must degrade to an approximate answer long before it
    /// holds up a booking.
    /// </summary>
    [Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Time-to-live for a cached origin-destination leg. Short by design:
    /// these durations are traffic-aware, so a value cached for minutes stops
    /// describing the road it came from. Sixty seconds keeps the repeated
    /// pings of one in-progress job off the API while staying inside the
    /// window in which traffic barely moves. Zero disables caching entirely.
    /// </summary>
    [Range(0, 3600)]
    public int CacheTtlSeconds { get; set; } = 60;

    /// <summary>
    /// Destinations per HTTP request. Requests are chunked to this size; with
    /// a single origin it is also the element count, so it can never exceed
    /// <see cref="MaxElementsPerRequestLimit"/>. Twenty-five keeps a single
    /// request's latency bounded while still batching a realistic candidate
    /// set into one round trip.
    /// </summary>
    [Range(1, MaxElementsPerRequestLimit)]
    public int MaxDestinationsPerCall { get; set; } = 25;

    /// <summary>
    /// Total destinations one <c>EstimateAsync</c> call may send to Google,
    /// across all its chunks. This is the cost circuit breaker: without it a
    /// caller that accidentally passes an unfiltered candidate list turns one
    /// booking into hundreds of billed elements. Destinations past the cap are
    /// not dropped - they get the sandbox estimate, and the overflow is
    /// logged as a warning so the offending caller is visible.
    /// </summary>
    [Range(1, 500)]
    public int MaxDestinationsPerEstimate { get; set; } = 100;

    /// <summary>
    /// True when real routing should be used: the integration is switched on
    /// and a key exists. Mirrors <see cref="CacheOptions.IsRedisConfigured"/>.
    /// </summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
