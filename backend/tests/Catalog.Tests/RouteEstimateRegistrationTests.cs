using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nestly.Application.Routing;
using Nestly.Infrastructure;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Services;
using Xunit;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 266's configuration-driven selection through the real
/// <c>AddInfrastructure</c> graph - the one part of this feature that a
/// compiler cannot check, because picking the wrong implementation (or
/// failing to construct it at all) only shows up when the container resolves
/// it. No HTTP is issued: resolving the provider does not call Google.
/// </summary>
public sealed class RouteEstimateRegistrationTests
{
    private static ServiceProvider BuildContainer(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new("ConnectionStrings:Database", "Host=localhost;Database=nestly_wiring_test;Username=nestly;Password=nestly"),
                // Registration only - nothing here opens a connection.
                new("BackgroundJobs:ServerEnabled", "false"),
                new("BackgroundJobs:DashboardEnabled", "false"),
                .. settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value))
            ])
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
    }

    [Fact]
    public void Resolves_the_sandbox_estimator_when_no_api_key_is_configured()
    {
        // The default posture: the system is runnable with no Google billing
        // account at all.
        using var container = BuildContainer();

        container.GetRequiredService<IRouteEstimateProvider>()
            .Should().BeOfType<SandboxRouteEstimateProvider>();
    }

    [Fact]
    public void Resolves_google_maps_when_an_api_key_is_configured()
    {
        using var container = BuildContainer(("GoogleMaps:ApiKey", "AIzaSy-not-a-real-key-000000000000000000"));

        container.GetRequiredService<IRouteEstimateProvider>()
            .Should().BeOfType<GoogleMapsRouteEstimateProvider>();
    }

    [Fact]
    public void Resolves_the_sandbox_estimator_when_the_kill_switch_is_off_despite_a_key()
    {
        // The lever ops pulls during a billing incident or a Google outage,
        // without deleting the credential from the secret store.
        using var container = BuildContainer(
            ("GoogleMaps:ApiKey", "AIzaSy-not-a-real-key-000000000000000000"),
            ("GoogleMaps:Enabled", "false"));

        container.GetRequiredService<IRouteEstimateProvider>()
            .Should().BeOfType<SandboxRouteEstimateProvider>();
    }

    [Fact]
    public void Registers_the_named_http_client_the_google_provider_uses()
    {
        // Named rather than typed so task 286 can swap the handler for a stub
        // and drive the failure matrix without a network.
        using var container = BuildContainer();

        using var client = container.GetRequiredService<IHttpClientFactory>()
            .CreateClient(GoogleMapsRouteEstimateProvider.HttpClientName);

        client.Timeout.Should().Be(TimeSpan.FromSeconds(new GoogleMapsOptions().TimeoutSeconds));
    }
}
