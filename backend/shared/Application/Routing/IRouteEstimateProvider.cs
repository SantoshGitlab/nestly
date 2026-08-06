namespace Nestly.Application.Routing;

/// <summary>
/// Road-network travel estimates from one origin to many destinations (task
/// 266) - the single seam through which this system asks "how far, and how
/// long, by road?". Auto-assignment ranks candidate providers with it (task
/// 267) and booking tracking computes the customer-facing ETA with it (task
/// 271), so both answer with the same road distances rather than each
/// inventing its own straight-line approximation.
///
/// Two implementations exist, selected by configuration:
/// <c>GoogleMapsRouteEstimateProvider</c> (real routing, needs a billed API
/// key) and <c>SandboxRouteEstimateProvider</c> (Haversine x a road-winding
/// factor, needs nothing) - the same swap-the-registration convention as
/// <see cref="Nestly.Application.Payments.IPaymentGateway"/>. The system is
/// fully runnable and testable on the sandbox alone.
/// </summary>
public interface IRouteEstimateProvider
{
    /// <summary>
    /// Estimates the road leg from <paramref name="origin"/> to each entry of
    /// <paramref name="destinations"/>, in one batched round trip.
    /// </summary>
    /// <returns>
    /// Exactly one estimate per destination, in the same order, with
    /// <see cref="RouteEstimate.DestinationIndex"/> pointing back at the input
    /// position. Never empty unless <paramref name="destinations"/> is.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>This method does not throw.</b> Every external failure - no API key,
    /// HTTP error, quota exhaustion, timeout, malformed response, or a single
    /// destination the router cannot reach - degrades to the sandbox estimate
    /// for the affected destinations and is logged. A tracking screen showing
    /// an approximate ETA is an acceptable outcome; a failed booking is not.
    /// Callers therefore need no try/catch and no null handling, but should
    /// read <see cref="RouteEstimate.Source"/> when the distinction matters
    /// (task 271 persists it as the ETA's provenance).
    /// </para>
    /// <para>
    /// The one exception is cooperative cancellation: when
    /// <paramref name="cancellationToken"/> is signalled, an
    /// <see cref="OperationCanceledException"/> propagates as usual. That is
    /// the caller's own request going away, not a provider failure, and
    /// swallowing it would defeat graceful shutdown.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<RouteEstimate>> EstimateAsync(
        GeoCoordinate origin,
        IReadOnlyList<GeoCoordinate> destinations,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A WGS84 point. Both components are required - the "coordinates may be
/// missing" case that <see cref="Nestly.BuildingBlocks.Geo.GeoDistance"/>
/// models with nullables is resolved by the caller before it gets here, so a
/// routing request is never half a point.
/// </summary>
public readonly record struct GeoCoordinate(decimal Latitude, decimal Longitude);

/// <summary>
/// Which implementation produced an estimate. Persisted by task 271 as the
/// ETA's provenance, so a support agent can tell a real traffic-aware ETA
/// from an approximation produced while the maps integration was degraded.
/// </summary>
public enum RouteEstimateSource
{
    /// <summary>Haversine x a road-winding factor at a fixed average speed - an approximation.</summary>
    Sandbox = 0,

    /// <summary>Google Maps road routing.</summary>
    GoogleMaps = 1
}

/// <summary>
/// The estimated road leg to one destination.
/// </summary>
/// <param name="DestinationIndex">Position of this destination in the request's destination list.</param>
/// <param name="DistanceMetres">Road distance, metres.</param>
/// <param name="DurationSeconds">Travel time, seconds.</param>
/// <param name="Source">Which implementation produced this particular estimate - it can differ per destination within one response when only some destinations degraded.</param>
public sealed record RouteEstimate(
    int DestinationIndex,
    int DistanceMetres,
    int DurationSeconds,
    RouteEstimateSource Source);
