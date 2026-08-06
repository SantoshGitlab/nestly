using Microsoft.Extensions.Options;
using Nestly.Application.Routing;
using Nestly.BuildingBlocks.Geo;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Sandbox <see cref="IRouteEstimateProvider"/> (task 266): great-circle
/// distance scaled by a road-winding factor, divided by an average speed. It
/// needs no credentials, no network and no billing account, which is what
/// makes the whole tracking and auto-assignment stack runnable and testable
/// on a laptop - the same reason <see cref="SandboxPaymentGateway"/> and
/// <see cref="SandboxPushNotificationProvider"/> exist.
///
/// It is also the safety net underneath the real integration:
/// <see cref="GoogleMapsRouteEstimateProvider"/> delegates to this class for
/// any destination Google could not answer for, which is why every method
/// here is deterministic, allocation-light and incapable of failing.
/// </summary>
public sealed class SandboxRouteEstimateProvider : IRouteEstimateProvider
{
    private const decimal MetresPerKilometre = 1_000m;
    private const decimal SecondsPerHour = 3_600m;

    /// <summary>
    /// Floor applied to the configured speed. The <c>[Range]</c> on
    /// <see cref="SandboxRouteEstimateOptions.AverageSpeedKph"/> already
    /// rejects zero, but this class's contract is that it cannot fail, and a
    /// DivideByZeroException reaching a booking would break exactly the
    /// promise the sandbox exists to keep.
    /// </summary>
    private const decimal MinimumSpeedKph = 1m;

    private readonly SandboxRouteEstimateOptions _options;

    public SandboxRouteEstimateProvider(IOptions<SandboxRouteEstimateOptions> options)
    {
        _options = options.Value;
    }

    public Task<IReadOnlyList<RouteEstimate>> EstimateAsync(
        GeoCoordinate origin,
        IReadOnlyList<GeoCoordinate> destinations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destinations);

        var estimates = new RouteEstimate[destinations.Count];
        for (int index = 0; index < destinations.Count; index++)
        {
            estimates[index] = Estimate(origin, destinations[index], index);
        }

        return Task.FromResult<IReadOnlyList<RouteEstimate>>(estimates);
    }

    /// <summary>
    /// Estimates a single leg. Exposed so
    /// <see cref="GoogleMapsRouteEstimateProvider"/> can fill in one degraded
    /// destination without re-batching the whole request.
    /// </summary>
    public RouteEstimate Estimate(GeoCoordinate origin, GeoCoordinate destination, int destinationIndex)
    {
        // GeoCoordinate's components are non-nullable, so GeoDistance can only
        // return null for an input this type cannot express; the coalesce is
        // for the compiler, not for a reachable case.
        decimal straightLineMetres = GeoDistance.MetresBetween(
            origin.Latitude, origin.Longitude, destination.Latitude, destination.Longitude) ?? 0m;

        decimal roadMetres = straightLineMetres * _options.RoadWindingFactor;
        decimal metresPerSecond = Math.Max(_options.AverageSpeedKph, MinimumSpeedKph) * MetresPerKilometre / SecondsPerHour;

        return new RouteEstimate(
            destinationIndex,
            ToWholeUnits(roadMetres),
            ToWholeUnits(roadMetres / metresPerSecond),
            RouteEstimateSource.Sandbox);
    }

    private static int ToWholeUnits(decimal value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
