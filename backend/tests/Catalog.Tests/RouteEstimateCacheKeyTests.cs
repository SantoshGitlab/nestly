using System.Globalization;
using FluentAssertions;
using Nestly.Application.Abstractions.Caching;
using Nestly.BuildingBlocks.Geo;
using Xunit;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 266's cache-key rounding. The point of rounding is that a
/// provider whose phone reports a slightly different fix every few seconds
/// keeps hitting the same cached leg instead of billing a fresh routing call
/// per ping - so these tests pin both halves of that trade: near-identical
/// points collapse, genuinely different points do not.
/// </summary>
public sealed class RouteEstimateCacheKeyTests
{
    private const decimal OriginLatitude = 12.9716m;
    private const decimal OriginLongitude = 77.5946m;
    private const decimal DestinationLatitude = 12.9352m;
    private const decimal DestinationLongitude = 77.6245m;

    [Fact]
    public void RouteEstimate_is_namespaced_under_the_shared_prefix_and_its_own_area()
    {
        var key = CacheKeys.RouteEstimate(OriginLatitude, OriginLongitude, DestinationLatitude, DestinationLongitude);

        key.Should().StartWith($"nestly:{CacheKeys.Areas.RouteEstimate}:");
    }

    [Fact]
    public void RouteEstimate_rounds_every_coordinate_to_the_documented_precision()
    {
        var key = CacheKeys.RouteEstimate(12.97164321m, 77.59461234m, -12.34565m, -77.00001m);

        key.Should().Be("nestly:route-estimate:12.9716:77.5946:-12.3457:-77.0000");
    }

    [Fact]
    public void RouteEstimate_collapses_movement_below_the_rounding_precision_onto_one_key()
    {
        var first = CacheKeys.RouteEstimate(12.97161m, 77.59462m, DestinationLatitude, DestinationLongitude);
        var second = CacheKeys.RouteEstimate(12.97164m, 77.59464m, DestinationLatitude, DestinationLongitude);

        second.Should().Be(first);
    }

    [Fact]
    public void RouteEstimate_separates_movement_above_the_rounding_precision()
    {
        var first = CacheKeys.RouteEstimate(OriginLatitude, OriginLongitude, DestinationLatitude, DestinationLongitude);
        var second = CacheKeys.RouteEstimate(12.9717m, OriginLongitude, DestinationLatitude, DestinationLongitude);

        second.Should().NotBe(first);
    }

    [Fact]
    public void RouteEstimate_rounding_step_stays_within_roughly_twelve_metres()
    {
        // Ties the chosen precision to the metre figure it is documented with:
        // one step at the fourth decimal place is ~11 m of latitude, well
        // inside the uncertainty of the GPS fix that produced the coordinate.
        decimal step = 1m;
        for (int place = 0; place < CacheKeys.RouteEstimateCoordinateDecimals; place++)
        {
            step /= 10m;
        }

        var metres = GeoDistance.MetresBetween(OriginLatitude, OriginLongitude, OriginLatitude + step, OriginLongitude);

        metres.Should().BeInRange(10m, 12m);
    }

    [Fact]
    public void RouteEstimate_is_direction_sensitive()
    {
        // Road legs are not symmetric (one-way streets, divided carriageways),
        // so A->B must not read B->A's cached duration.
        var outbound = CacheKeys.RouteEstimate(OriginLatitude, OriginLongitude, DestinationLatitude, DestinationLongitude);
        var inbound = CacheKeys.RouteEstimate(DestinationLatitude, DestinationLongitude, OriginLatitude, OriginLongitude);

        inbound.Should().NotBe(outbound);
    }

    [Fact]
    public void RouteEstimate_is_culture_invariant()
    {
        // A replica running under a comma-decimal culture must derive
        // byte-identical keys, or the fleet quietly keeps separate caches.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var invariantKey = CacheKeys.RouteEstimate(OriginLatitude, OriginLongitude, DestinationLatitude, DestinationLongitude);

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var germanKey = CacheKeys.RouteEstimate(OriginLatitude, OriginLongitude, DestinationLatitude, DestinationLongitude);

            germanKey.Should().Be(invariantKey);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void RouteEstimate_never_carries_a_credential()
    {
        // Cache keys reach Redis, redis-cli output and this application's own
        // cache-miss logs. Only coordinates may appear in one.
        var key = CacheKeys.RouteEstimate(OriginLatitude, OriginLongitude, DestinationLatitude, DestinationLongitude);

        key.Split(':').Should().HaveCount(6);
    }
}
