using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Application.Routing;
using Nestly.BuildingBlocks.Geo;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Services;
using Xunit;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 266's sandbox estimator - the implementation that keeps
/// tracking, ETA and auto-assignment runnable with no Google Maps key, and
/// that the real provider degrades to destination-by-destination. Its maths
/// needs no HTTP, so it is tested here; the stubbed-HTTP failure matrix for
/// the Google implementation is task 286.
/// </summary>
public sealed class SandboxRouteEstimateProviderTests
{
    private const decimal BengaluruLatitude = 12.9716m;
    private const decimal BengaluruLongitude = 77.5946m;
    private const decimal ChennaiLatitude = 13.0827m;
    private const decimal ChennaiLongitude = 80.2707m;

    /// <summary>Published great-circle distance for the pair above (see GeoDistanceTests).</summary>
    private const decimal BengaluruToChennaiKilometres = 290.2m;

    private static readonly GeoCoordinate Bengaluru = new(BengaluruLatitude, BengaluruLongitude);
    private static readonly GeoCoordinate Chennai = new(ChennaiLatitude, ChennaiLongitude);

    private static SandboxRouteEstimateProvider Provider(decimal roadWindingFactor = 1.3m, decimal averageSpeedKph = 25m) =>
        new(Options.Create(new SandboxRouteEstimateOptions
        {
            RoadWindingFactor = roadWindingFactor,
            AverageSpeedKph = averageSpeedKph
        }));

    [Fact]
    public void Estimate_returns_zero_distance_and_duration_for_identical_points()
    {
        var estimate = Provider().Estimate(Bengaluru, Bengaluru, destinationIndex: 0);

        estimate.DistanceMetres.Should().Be(0);
        estimate.DurationSeconds.Should().Be(0);
    }

    [Fact]
    public void Estimate_scales_the_straight_line_distance_by_the_road_winding_factor()
    {
        var estimate = Provider(roadWindingFactor: 1.3m).Estimate(Bengaluru, Chennai, destinationIndex: 0);

        decimal expectedMetres = BengaluruToChennaiKilometres * 1000m * 1.3m;

        // Tolerance absorbs the spherical-earth approximation in the published
        // reference distance, not our own arithmetic.
        estimate.DistanceMetres.Should().BeInRange((int)(expectedMetres - 1000m), (int)(expectedMetres + 1000m));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.3)]
    [InlineData(2.5)]
    public void Estimate_applies_the_configured_winding_factor_exactly(decimal roadWindingFactor)
    {
        decimal straightLineMetres =
            GeoDistance.MetresBetween(BengaluruLatitude, BengaluruLongitude, ChennaiLatitude, ChennaiLongitude)!.Value;

        var estimate = Provider(roadWindingFactor).Estimate(Bengaluru, Chennai, destinationIndex: 0);

        estimate.DistanceMetres.Should().Be((int)Math.Round(straightLineMetres * roadWindingFactor, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void Estimate_derives_duration_from_the_configured_average_speed()
    {
        // 36 km/h is exactly 10 m/s, so the expected duration is the road
        // distance divided by ten with no rounding argument to make.
        var estimate = Provider(roadWindingFactor: 1m, averageSpeedKph: 36m).Estimate(Bengaluru, Chennai, destinationIndex: 0);

        estimate.DurationSeconds.Should().Be((int)Math.Round(estimate.DistanceMetres / 10m, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void Estimate_returns_a_longer_journey_for_a_slower_average_speed()
    {
        var fast = Provider(averageSpeedKph: 50m).Estimate(Bengaluru, Chennai, destinationIndex: 0);
        var slow = Provider(averageSpeedKph: 25m).Estimate(Bengaluru, Chennai, destinationIndex: 0);

        slow.DurationSeconds.Should().BeGreaterThan(fast.DurationSeconds);
    }

    [Fact]
    public void Estimate_reports_the_sandbox_as_its_source()
    {
        // Task 271 persists this as the ETA's provenance, so an approximate
        // ETA is never presented as a real traffic-aware one.
        Provider().Estimate(Bengaluru, Chennai, destinationIndex: 0).Source
            .Should().Be(RouteEstimateSource.Sandbox);
    }

    [Fact]
    public void Estimate_does_not_divide_by_zero_when_the_configured_speed_is_zero()
    {
        // DataAnnotations already rejects a zero speed; this asserts the
        // defence underneath it, because a route estimate must never throw
        // out to a booking or a tracking screen.
        var estimate = Provider(averageSpeedKph: 0m).Estimate(Bengaluru, Chennai, destinationIndex: 0);

        estimate.DurationSeconds.Should().BePositive();
    }

    [Fact]
    public async Task EstimateAsync_returns_one_estimate_per_destination_in_request_order()
    {
        GeoCoordinate[] destinations =
        [
            Chennai,
            new(28.6139m, 77.2090m),
            Bengaluru
        ];

        var estimates = await Provider().EstimateAsync(Bengaluru, destinations);

        estimates.Should().HaveCount(3);
        estimates.Select(e => e.DestinationIndex).Should().Equal(0, 1, 2);

        // Index alignment is the contract task 267 ranks on - the third
        // destination is the origin itself, so it must be the zero-length leg.
        estimates[2].DistanceMetres.Should().Be(0);
    }

    [Fact]
    public async Task EstimateAsync_returns_nothing_for_an_empty_destination_list()
    {
        var estimates = await Provider().EstimateAsync(Bengaluru, []);

        estimates.Should().BeEmpty();
    }
}
