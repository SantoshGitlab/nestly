using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.Abstractions.Caching;
using Nestly.Application.Routing;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Real <see cref="IRouteEstimateProvider"/> backed by the Google Maps
/// Platform <b>Routes API</b> (<c>computeRouteMatrix</c>), task 266.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why Routes API and not the Distance Matrix API.</b> Google made
/// Distance Matrix a legacy product on 1 March 2025: existing projects keep
/// working, but it cannot be enabled on a Cloud project that had not already
/// turned it on. Every deployer of this system will be creating a fresh
/// project, so a Distance Matrix implementation would be one nobody could
/// switch on. <c>computeRouteMatrix</c> is its documented successor, takes
/// the API key in a header instead of the query string (see key hygiene
/// below), and lets a field mask limit the response - and therefore the bill
/// - to the four fields this class actually reads.
/// </para>
/// <para>
/// <b>Key hygiene.</b> The key exists in exactly one place in this class: the
/// <c>X-Goog-Api-Key</c> header. It is never in a URL (so it cannot reach
/// request-URI logs or an <see cref="HttpRequestException"/> message), never
/// in a cache key, and never in a log statement - the diagnostics below carry
/// status codes and destination counts only, and deliberately do not echo the
/// response body.
/// </para>
/// <para>
/// <b>This class does not throw.</b> Every failure path narrows to "use
/// <see cref="SandboxRouteEstimateProvider"/> for the affected destinations
/// and log it" - see <see cref="IRouteEstimateProvider"/>. All HTTP detail is
/// confined here, so replacing Google with another routing vendor is a
/// one-class change.
/// </para>
/// </remarks>
public sealed class GoogleMapsRouteEstimateProvider : IRouteEstimateProvider
{
    /// <summary>
    /// Named <see cref="HttpClient"/> registration. Named rather than typed so
    /// tests can supply their own <see cref="HttpMessageHandler"/> through
    /// <see cref="IHttpClientFactory"/> and exercise this class with no
    /// network (task 286).
    /// </summary>
    public const string HttpClientName = "GoogleMaps.Routes";

    private const string ComputeRouteMatrixUri = "https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix";
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string FieldMaskHeaderName = "X-Goog-FieldMask";

    /// <summary>
    /// Routes API bills partly by the fields returned, so this asks for
    /// exactly what is read below and nothing else.
    /// </summary>
    private const string FieldMask = "originIndex,destinationIndex,distanceMeters,duration,condition,status";

    private const string TravelMode = "DRIVE";

    /// <summary>
    /// TRAFFIC_AWARE, not TRAFFIC_AWARE_OPTIMAL: an ETA the customer watches
    /// needs live traffic, but not at OPTIMAL's higher price and tighter
    /// 100-element ceiling. Not TRAFFIC_UNAWARE either - a free-flow ETA on a
    /// congested city road is the exact number this feature exists to stop
    /// showing.
    /// </summary>
    private const string RoutingPreference = "TRAFFIC_AWARE";

    private const string MetricUnits = "IMPERIAL";

    /// <summary>Per-element condition meaning a route was found.</summary>
    private const string RouteExistsCondition = "ROUTE_EXISTS";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICacheService _cache;
    private readonly SandboxRouteEstimateProvider _sandbox;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<GoogleMapsRouteEstimateProvider> _logger;

    public GoogleMapsRouteEstimateProvider(
        IHttpClientFactory httpClientFactory,
        ICacheService cache,
        SandboxRouteEstimateProvider sandbox,
        IOptions<GoogleMapsOptions> options,
        ILogger<GoogleMapsRouteEstimateProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _sandbox = sandbox;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RouteEstimate>> EstimateAsync(
        GeoCoordinate origin,
        IReadOnlyList<GeoCoordinate> destinations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destinations);

        if (destinations.Count == 0)
        {
            return [];
        }

        if (!_options.IsConfigured)
        {
            // Registration already picks the sandbox when no key is present,
            // so reaching here means configuration changed under a live
            // process. Degrade rather than fail, and say so once per call.
            _logger.LogWarning(
                "Google Maps routing is not configured (missing key or disabled); estimating {DestinationCount} destination(s) with the sandbox estimator.",
                destinations.Count);
            return await _sandbox.EstimateAsync(origin, destinations, cancellationToken).ConfigureAwait(false);
        }

        var estimates = new RouteEstimate?[destinations.Count];
        var pending = await ReadFromCacheAsync(origin, destinations, estimates, cancellationToken).ConfigureAwait(false);

        if (pending.Count > 0)
        {
            var plan = RouteMatrixBatchPlanner.Plan(pending, _options);

            if (plan.Overflow.Count > 0)
            {
                _logger.LogWarning(
                    "Route estimate for {DestinationCount} destination(s) exceeds the configured cap of {MaxDestinations}; {OverflowCount} will use the sandbox estimator instead of Google.",
                    destinations.Count,
                    _options.MaxDestinationsPerEstimate,
                    plan.Overflow.Count);
            }

            foreach (var batch in plan.Batches)
            {
                await ApplyBatchAsync(origin, destinations, batch, estimates, cancellationToken).ConfigureAwait(false);
            }
        }

        return FillRemainingFromSandbox(origin, destinations, estimates);
    }

    /// <summary>
    /// Resolves what the cache already knows and returns the destination
    /// indexes still needing a call.
    /// </summary>
    private async Task<List<int>> ReadFromCacheAsync(
        GeoCoordinate origin,
        IReadOnlyList<GeoCoordinate> destinations,
        RouteEstimate?[] estimates,
        CancellationToken cancellationToken)
    {
        var pending = new List<int>(destinations.Count);

        if (_options.CacheTtlSeconds <= 0)
        {
            for (int index = 0; index < destinations.Count; index++)
            {
                pending.Add(index);
            }

            return pending;
        }

        for (int index = 0; index < destinations.Count; index++)
        {
            var cached = await _cache
                .GetAsync<CachedRouteLeg>(CacheKeyFor(origin, destinations[index]), cancellationToken)
                .ConfigureAwait(false);

            if (cached is null)
            {
                pending.Add(index);
            }
            else
            {
                estimates[index] = new RouteEstimate(
                    index, cached.DistanceMetres, cached.DurationSeconds, RouteEstimateSource.GoogleMaps);
            }
        }

        return pending;
    }

    /// <summary>
    /// Routes one batch and writes whatever came back into
    /// <paramref name="estimates"/>. Anything it cannot answer is simply left
    /// unset, for <see cref="FillRemainingFromSandbox"/> to pick up.
    /// </summary>
    private async Task ApplyBatchAsync(
        GeoCoordinate origin,
        IReadOnlyList<GeoCoordinate> destinations,
        IReadOnlyList<int> batch,
        RouteEstimate?[] estimates,
        CancellationToken cancellationToken)
    {
        var batchDestinations = new GeoCoordinate[batch.Count];
        for (int position = 0; position < batch.Count; position++)
        {
            batchDestinations[position] = destinations[batch[position]];
        }

        var elements = await TryComputeRouteMatrixAsync(origin, batchDestinations, cancellationToken).ConfigureAwait(false);
        if (elements is null)
        {
            return;
        }

        foreach (var element in elements)
        {
            if (element is null || element.DestinationIndex < 0 || element.DestinationIndex >= batch.Count)
            {
                _logger.LogWarning("Google Maps returned a route matrix element outside the requested destination range; ignoring it.");
                continue;
            }

            int destinationIndex = batch[element.DestinationIndex];

            if (!TryReadLeg(element, out var leg))
            {
                _logger.LogInformation(
                    "Google Maps could not route one destination (condition {RouteCondition}, status {StatusCode}); using the sandbox estimate for it.",
                    element.Condition ?? "none",
                    element.Status?.Code ?? 0);
                continue;
            }

            estimates[destinationIndex] = new RouteEstimate(
                destinationIndex, leg.DistanceMetres, leg.DurationSeconds, RouteEstimateSource.GoogleMaps);

            await WriteToCacheAsync(origin, destinations[destinationIndex], leg, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Issues one <c>computeRouteMatrix</c> request.
    /// </summary>
    /// <returns>
    /// The elements, or <c>null</c> when the call failed for any reason -
    /// which is the whole point of this method: it converts every possible
    /// external fault into one "no answer" signal that the caller degrades on.
    /// </returns>
    private async Task<List<RouteMatrixElementResponse>?> TryComputeRouteMatrixAsync(
        GeoCoordinate origin,
        IReadOnlyList<GeoCoordinate> destinations,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var request = new HttpRequestMessage(HttpMethod.Post, ComputeRouteMatrixUri);

            // The key travels as a header, never as a query parameter, so it
            // cannot leak through the request URI into logs or exceptions.
            request.Headers.TryAddWithoutValidation(ApiKeyHeaderName, _options.ApiKey);
            request.RequestUri = new Uri(ComputeRouteMatrixUri + "?key=" + _options.ApiKey);
            request.Headers.TryAddWithoutValidation(FieldMaskHeaderName, FieldMask);
            request.Content = JsonContent.Create(BuildRequestBody(origin, destinations), options: SerializerOptions);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogUnsuccessfulResponse(response.StatusCode, destinations.Count);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<List<RouteMatrixElementResponse>>(SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller's own request went away. That is cooperative
            // cancellation, not a provider failure - swallowing it would keep
            // shutdown and client-disconnect handling from working.
            throw;
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogWarning(
                exception,
                "Google Maps route matrix timed out after {TimeoutSeconds}s for {DestinationCount} destination(s); falling back to the sandbox estimator.",
                _options.TimeoutSeconds,
                destinations.Count);
            return null;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Google Maps route matrix request failed for {DestinationCount} destination(s); falling back to the sandbox estimator.",
                destinations.Count);
            return null;
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "Google Maps route matrix returned a body this application could not parse for {DestinationCount} destination(s); falling back to the sandbox estimator.",
                destinations.Count);
            return null;
        }
        catch (Exception exception)
        {
            // Deliberate catch-all, not a swallow: this method's contract is
            // that no external fault reaches a booking or a tracking screen.
            // It is logged at Error precisely because an unclassified failure
            // here is worth investigating.
            _logger.LogError(
                exception,
                "Unexpected failure calling Google Maps route matrix for {DestinationCount} destination(s); falling back to the sandbox estimator.",
                destinations.Count);
            return null;
        }
    }

    /// <summary>
    /// Grades an unsuccessful HTTP status. Quota exhaustion is a warning - it
    /// is expected under load and self-heals; a rejected key is an error,
    /// because it stays broken until someone fixes the credential.
    /// </summary>
    private void LogUnsuccessfulResponse(HttpStatusCode statusCode, int destinationCount)
    {
        const string MessageTemplate =
            "Google Maps route matrix returned {StatusCode} for {DestinationCount} destination(s); falling back to the sandbox estimator.";

        switch (statusCode)
        {
            case HttpStatusCode.TooManyRequests:
                _logger.LogWarning(MessageTemplate, (int)statusCode, destinationCount);
                break;

            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                _logger.LogError(
                    "Google Maps rejected this application's credential ({StatusCode}) for {DestinationCount} destination(s) - check that the server-side key is enabled for the Routes API and that its IP restriction covers this host. Falling back to the sandbox estimator.",
                    (int)statusCode,
                    destinationCount);
                break;

            default:
                _logger.LogError(MessageTemplate, (int)statusCode, destinationCount);
                break;
        }
    }

    /// <summary>
    /// Validates one element and converts it to whole metres and seconds.
    /// Returns false for a route that does not exist, an element-level error
    /// status, or a duration this application cannot read.
    /// </summary>
    private static bool TryReadLeg(RouteMatrixElementResponse element, out CachedRouteLeg leg)
    {
        leg = default!;

        // An element carries its own status; a non-zero google.rpc.Status code
        // means this pair failed even though the request as a whole succeeded.

        if (!string.Equals(element.Condition, RouteExistsCondition, StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryParseDurationSeconds(element.Duration, out int durationSeconds))
        {
            return false;
        }

        // distanceMeters is omitted rather than sent as 0 for a zero-length
        // leg (proto3 drops default values), so absent means zero here.
        leg = new CachedRouteLeg(Math.Max(element.DistanceMeters ?? 0, 0), durationSeconds);
        return true;
    }

    /// <summary>
    /// Parses a protobuf Duration ("123s", "123.5s") into whole seconds.
    /// </summary>
    private static bool TryParseDurationSeconds(string? duration, out int seconds)
    {
        seconds = 0;

        if (string.IsNullOrWhiteSpace(duration) || !duration.EndsWith('s'))
        {
            return false;
        }

        if (!double.TryParse(duration[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ||
            double.IsNaN(parsed) || parsed < 0 || parsed > int.MaxValue)
        {
            return false;
        }

        seconds = (int)Math.Round(parsed, MidpointRounding.AwayFromZero);
        return true;
    }

    private async Task WriteToCacheAsync(
        GeoCoordinate origin,
        GeoCoordinate destination,
        CachedRouteLeg leg,
        CancellationToken cancellationToken)
    {
        if (_options.CacheTtlSeconds <= 0)
        {
            return;
        }

        await _cache.SetAsync(
            CacheKeyFor(origin, destination),
            leg,
            TimeSpan.FromSeconds(_options.CacheTtlSeconds),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fills every destination Google did not answer for with the sandbox
    /// estimate, so the caller always receives one estimate per destination.
    /// Sandbox values are never cached: they are cheap to recompute, and
    /// caching them would leave approximations in place after the outage that
    /// produced them had passed.
    /// </summary>
    private IReadOnlyList<RouteEstimate> FillRemainingFromSandbox(
        GeoCoordinate origin,
        IReadOnlyList<GeoCoordinate> destinations,
        RouteEstimate?[] estimates)
    {
        var resolved = new RouteEstimate[estimates.Length];
        for (int index = 0; index < estimates.Length; index++)
        {
            resolved[index] = estimates[index] ?? _sandbox.Estimate(origin, destinations[index], index);
        }

        return resolved;
    }

    private static string CacheKeyFor(GeoCoordinate origin, GeoCoordinate destination) =>
        CacheKeys.RouteEstimate(origin.Latitude, origin.Longitude, destination.Latitude, destination.Longitude);

    private static RouteMatrixRequest BuildRequestBody(GeoCoordinate origin, IReadOnlyList<GeoCoordinate> destinations) =>
        new(
            [ToWaypoint(origin)],
            destinations.Select(ToWaypoint).ToArray(),
            TravelMode,
            RoutingPreference,
            MetricUnits);

    private static RouteMatrixWaypointEnvelope ToWaypoint(GeoCoordinate coordinate) =>
        new(new RouteMatrixWaypoint(new RouteMatrixLocation(
            new RouteMatrixLatLng(coordinate.Latitude, coordinate.Longitude))));
}

/// <summary>
/// <c>computeRouteMatrix</c> request body (task 266). Modelled explicitly
/// rather than as anonymous objects so the wire shape this application
/// depends on is readable in one place, and so serialization never has to
/// infer it from a runtime type.
/// </summary>
internal sealed record RouteMatrixRequest(
    IReadOnlyList<RouteMatrixWaypointEnvelope> Origins,
    IReadOnlyList<RouteMatrixWaypointEnvelope> Destinations,
    string TravelMode,
    string RoutingPreference,
    string Units);

internal sealed record RouteMatrixWaypointEnvelope(RouteMatrixWaypoint Waypoint);

internal sealed record RouteMatrixWaypoint(RouteMatrixLocation Location);

internal sealed record RouteMatrixLocation(RouteMatrixLatLng LatLng);

internal sealed record RouteMatrixLatLng(decimal Latitude, decimal Longitude);

/// <summary>One element of a <c>computeRouteMatrix</c> response.</summary>
internal sealed record RouteMatrixElementResponse(
    int OriginIndex,
    int DestinationIndex,
    int? DistanceMeters,
    string? Duration,
    string? Condition,
    RouteMatrixStatusResponse? Status);

/// <summary>The <c>google.rpc.Status</c> carried by each element; code 0 (or an empty object) means OK.</summary>
internal sealed record RouteMatrixStatusResponse(int? Code, string? Message);

/// <summary>
/// Cached route leg (task 266). Only Google-sourced legs are ever written, so
/// a cache hit always restores
/// <see cref="RouteEstimateSource.GoogleMaps"/> and the source needs no field
/// of its own. Public because the cache serializes it with System.Text.Json.
/// </summary>
public sealed record CachedRouteLeg(int DistanceMetres, int DurationSeconds);
