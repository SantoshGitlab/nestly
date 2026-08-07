using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Nestly.Infrastructure.Options;
using Xunit;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 266's configuration surface: the defaults a deployment gets
/// for free, the ranges that stop a misconfiguration reaching Google, and the
/// key-hygiene guarantee that the options object itself cannot leak the
/// credential.
/// </summary>
public sealed class RouteEstimateOptionsTests
{
    private const string SampleApiKey = "AIzaSy-not-a-real-key-000000000000000000";

    private static IReadOnlyList<ValidationResult> Validate(object options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void GoogleMapsOptions_defaults_are_valid_and_need_no_configuration()
    {
        var options = new GoogleMapsOptions();

        options.ApiKey.Should().BeNull();
        options.Enabled.Should().BeTrue();
        options.TimeoutSeconds.Should().Be(5);
        options.CacheTtlSeconds.Should().Be(60);
        options.MaxDestinationsPerCall.Should().Be(25);
        options.MaxDestinationsPerEstimate.Should().Be(100);

        Validate(options).Should().BeEmpty();
    }

    [Fact]
    public void GoogleMapsOptions_is_not_configured_without_a_key()
    {
        // The whole point of the fallback: no key, no billing account, still
        // a working - if approximate - system.
        new GoogleMapsOptions().IsConfigured.Should().BeFalse();
        new GoogleMapsOptions { ApiKey = "   " }.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void GoogleMapsOptions_is_not_configured_when_the_kill_switch_is_off()
    {
        new GoogleMapsOptions { ApiKey = SampleApiKey, Enabled = false }.IsConfigured.Should().BeFalse();
        new GoogleMapsOptions { ApiKey = SampleApiKey }.IsConfigured.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void GoogleMapsOptions_rejects_a_timeout_outside_its_range(int timeoutSeconds)
    {
        Validate(new GoogleMapsOptions { TimeoutSeconds = timeoutSeconds })
            .Should().ContainSingle().Which.MemberNames.Should().Contain(nameof(GoogleMapsOptions.TimeoutSeconds));
    }

    [Fact]
    public void GoogleMapsOptions_rejects_a_negative_cache_ttl_but_allows_zero_to_disable_caching()
    {
        Validate(new GoogleMapsOptions { CacheTtlSeconds = -1 }).Should().ContainSingle();
        Validate(new GoogleMapsOptions { CacheTtlSeconds = 0 }).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GoogleMapsOptions.MaxElementsPerRequestLimit + 1)]
    public void GoogleMapsOptions_rejects_a_batch_size_google_would_refuse(int maxDestinationsPerCall)
    {
        Validate(new GoogleMapsOptions { MaxDestinationsPerCall = maxDestinationsPerCall })
            .Should().ContainSingle().Which.MemberNames.Should().Contain(nameof(GoogleMapsOptions.MaxDestinationsPerCall));
    }

    [Fact]
    public void GoogleMapsOptions_rejects_a_fan_out_cap_that_would_disable_the_circuit_breaker()
    {
        Validate(new GoogleMapsOptions { MaxDestinationsPerEstimate = 0 }).Should().ContainSingle();
        Validate(new GoogleMapsOptions { MaxDestinationsPerEstimate = 501 }).Should().ContainSingle();
    }

    [Fact]
    public void GoogleMapsOptions_does_not_print_the_api_key()
    {
        // Regression guard for the reason this is a class and not a record: a
        // record's synthesized ToString would put the credential into any log
        // statement that formatted the options object.
        var options = new GoogleMapsOptions { ApiKey = SampleApiKey };

        options.ToString().Should().NotContain(SampleApiKey);
    }

    [Fact]
    public void SandboxRouteEstimateOptions_defaults_are_valid_and_need_no_configuration()
    {
        var options = new SandboxRouteEstimateOptions();

        options.RoadWindingFactor.Should().Be(1.3m);
        options.AverageSpeedKph.Should().Be(25m);

        Validate(options).Should().BeEmpty();
    }

    [Fact]
    public void SandboxRouteEstimateOptions_rejects_a_winding_factor_that_shortens_the_journey()
    {
        // A road can only be longer than the straight line between its ends.
        Validate(new SandboxRouteEstimateOptions { RoadWindingFactor = 0.9m })
            .Should().ContainSingle().Which.MemberNames.Should().Contain(nameof(SandboxRouteEstimateOptions.RoadWindingFactor));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-25)]
    [InlineData(201)]
    public void SandboxRouteEstimateOptions_rejects_an_unusable_average_speed(decimal averageSpeedKph)
    {
        Validate(new SandboxRouteEstimateOptions { AverageSpeedKph = averageSpeedKph })
            .Should().ContainSingle().Which.MemberNames.Should().Contain(nameof(SandboxRouteEstimateOptions.AverageSpeedKph));
    }
}
