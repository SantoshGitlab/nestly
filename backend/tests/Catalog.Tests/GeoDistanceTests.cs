using FluentAssertions;
using Nestly.BuildingBlocks.Geo;
using Xunit;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 265: the shared Haversine helper lifted out of
/// ProviderMatchingService, now that candidate ranking, ETA and location-ingest
/// throttling all need the same maths.
/// </summary>
public sealed class GeoDistanceTests
{
    private const decimal BengaluruLatitude = 12.9716m;
    private const decimal BengaluruLongitude = 77.5946m;
    private const decimal ChennaiLatitude = 13.0827m;
    private const decimal ChennaiLongitude = 80.2707m;

    /// <summary>Half the earth's circumference at the 6371 km mean radius - the largest distance the helper can ever return.</summary>
    private const decimal AntipodalKilometres = 20015.0868m;

    [Theory]
    // Published great-circle distances, to a tolerance that absorbs the
    // spherical-earth approximation rather than pinning our own output.
    [InlineData(12.9716, 77.5946, 13.0827, 80.2707, 290.2)]      // Bengaluru - Chennai
    [InlineData(28.6139, 77.2090, 19.0760, 72.8777, 1148.1)]     // Delhi - Mumbai
    [InlineData(51.5074, -0.1278, 48.8566, 2.3522, 343.6)]       // London - Paris, across the prime meridian
    public void KilometresBetween_matches_known_city_pair_distances(
        decimal latitude1, decimal longitude1, decimal latitude2, decimal longitude2, decimal expectedKilometres)
    {
        var distance = GeoDistance.KilometresBetween(latitude1, longitude1, latitude2, longitude2);

        distance.Should().BeApproximately(expectedKilometres, 0.5m);
    }

    [Fact]
    public void KilometresBetween_returns_zero_for_identical_points()
    {
        var distance = GeoDistance.KilometresBetween(
            BengaluruLatitude, BengaluruLongitude, BengaluruLatitude, BengaluruLongitude);

        distance.Should().Be(0m);
    }

    [Fact]
    public void KilometresBetween_is_symmetric()
    {
        var outward = GeoDistance.KilometresBetween(BengaluruLatitude, BengaluruLongitude, ChennaiLatitude, ChennaiLongitude);
        var homeward = GeoDistance.KilometresBetween(ChennaiLatitude, ChennaiLongitude, BengaluruLatitude, BengaluruLongitude);

        outward.Should().Be(homeward);
    }

    /// <summary>InlineData cannot carry a <c>decimal?</c> - xUnit will not convert its double literals to a nullable target - so the incomplete pairs come through TheoryData instead.</summary>
    public static TheoryData<decimal?, decimal?, decimal?, decimal?> IncompleteCoordinatePairs => new()
    {
        { null, BengaluruLongitude, ChennaiLatitude, ChennaiLongitude },
        { BengaluruLatitude, null, ChennaiLatitude, ChennaiLongitude },
        { BengaluruLatitude, BengaluruLongitude, null, ChennaiLongitude },
        { BengaluruLatitude, BengaluruLongitude, ChennaiLatitude, null },
        { null, null, ChennaiLatitude, ChennaiLongitude },
        { BengaluruLatitude, BengaluruLongitude, null, null },
        { null, null, null, null },
    };

    [Theory]
    [MemberData(nameof(IncompleteCoordinatePairs))]
    public void Both_units_return_null_when_either_point_is_incomplete(
        decimal? latitude1, decimal? longitude1, decimal? latitude2, decimal? longitude2)
    {
        GeoDistance.KilometresBetween(latitude1, longitude1, latitude2, longitude2).Should().BeNull();
        GeoDistance.MetresBetween(latitude1, longitude1, latitude2, longitude2).Should().BeNull();
    }

    [Theory]
    [InlineData(0, 0, 0, 180)]                                  // across the equator
    [InlineData(90, 0, -90, 0)]                                 // pole to pole
    [InlineData(51.5074, -0.1278, -51.5074, 179.8722)]          // London and its antipode
    public void KilometresBetween_handles_antipodal_points_without_leaving_the_maths_domain(
        decimal latitude1, decimal longitude1, decimal latitude2, decimal longitude2)
    {
        var distance = GeoDistance.KilometresBetween(latitude1, longitude1, latitude2, longitude2);

        distance.Should().BeApproximately(AntipodalKilometres, 0.5m);
    }

    /// <summary>
    /// A hair off exact antipodal is where a naive implementation dies: the
    /// haversine term rounds a few ulps above 1, sqrt(1 - a) goes NaN, and the
    /// cast to decimal throws rather than returning ~20015 km.
    /// </summary>
    [Theory]
    [InlineData(0, 0, 0.000001, 179.999999)]
    [InlineData(0.000001, 0.000001, -0.000001, 179.999999)]
    [InlineData(89.999999, 0, -89.999999, 179.999999)]
    public void KilometresBetween_survives_near_antipodal_points(
        decimal latitude1, decimal longitude1, decimal latitude2, decimal longitude2)
    {
        var act = () => GeoDistance.KilometresBetween(latitude1, longitude1, latitude2, longitude2);

        act.Should().NotThrow().Which.Should().BeApproximately(AntipodalKilometres, 0.5m);
    }

    [Fact]
    public void MetresBetween_reports_the_same_distance_in_metres()
    {
        var kilometres = GeoDistance.KilometresBetween(BengaluruLatitude, BengaluruLongitude, ChennaiLatitude, ChennaiLongitude);
        var metres = GeoDistance.MetresBetween(BengaluruLatitude, BengaluruLongitude, ChennaiLatitude, ChennaiLongitude);

        metres.Should().BeApproximately(kilometres!.Value * 1000m, 0.001m);
    }

    /// <summary>
    /// The movement-threshold case metres exist for: roughly 108 m of eastward
    /// drift at Bengaluru's latitude, which in kilometres would be a fiddly
    /// 0.108 to threshold against.
    /// </summary>
    [Fact]
    public void MetresBetween_resolves_a_short_movement_a_throttle_would_test()
    {
        var metres = GeoDistance.MetresBetween(12.935200m, 77.624500m, 12.935200m, 77.625500m);

        metres.Should().BeApproximately(108.4m, 1m);
    }

    [Fact]
    public void MetresBetween_returns_zero_for_identical_points()
    {
        GeoDistance.MetresBetween(BengaluruLatitude, BengaluruLongitude, BengaluruLatitude, BengaluruLongitude)
            .Should().Be(0m);
    }
}
