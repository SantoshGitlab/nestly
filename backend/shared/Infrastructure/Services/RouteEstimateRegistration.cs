using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.Routing;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// <see cref="IRouteEstimateProvider"/> registration (task 266). Its own file,
/// same as <c>CachingRegistration</c>, because - like the cache - the
/// implementation is chosen by configuration rather than fixed, and that
/// choice deserves to be readable on its own.
/// </summary>
internal static class RouteEstimateRegistration
{
    /// <summary>
    /// Registers Google Maps routing when a key is configured and the sandbox
    /// estimator otherwise.
    /// </summary>
    /// <remarks>
    /// The sandbox is not merely the fallback registration: it is also a
    /// dependency of the Google implementation, which delegates to it for any
    /// destination Google could not answer for. So it is always registered as
    /// a concrete type, and only the <see cref="IRouteEstimateProvider"/>
    /// binding varies.
    /// </remarks>
    internal static IServiceCollection AddRouteEstimates(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Neither section holds anything a process cannot start without - the
        // key is optional by design - so no ValidateOnStart, same reasoning as
        // CommissionOptions and AutoAssignmentOptions.
        services
            .AddOptions<GoogleMapsOptions>()
            .Bind(configuration.GetSection(GoogleMapsOptions.SectionName))
            .ValidateDataAnnotations();

        services
            .AddOptions<SandboxRouteEstimateOptions>()
            .Bind(configuration.GetSection(SandboxRouteEstimateOptions.SectionName))
            .ValidateDataAnnotations();

        // A named client, so the timeout is enforced by the factory-managed
        // handler rather than by each call site remembering to bound itself -
        // and so tests can replace the handler without touching this class.
        services.AddHttpClient(GoogleMapsRouteEstimateProvider.HttpClientName, (serviceProvider, client) =>
        {
            var googleMapsOptions = serviceProvider.GetRequiredService<IOptions<GoogleMapsOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(googleMapsOptions.TimeoutSeconds);
        });

        // Stateless apart from bound options, same reasoning as
        // SandboxPaymentGateway - one shared instance is safe.
        services.AddSingleton<SandboxRouteEstimateProvider>();

        services.AddSingleton<IRouteEstimateProvider>(serviceProvider =>
        {
            var googleMapsOptions = serviceProvider.GetRequiredService<IOptions<GoogleMapsOptions>>().Value;
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RouteEstimateRegistration));

            if (!googleMapsOptions.IsConfigured)
            {
                // Logged at startup so "why is the ETA approximate?" is
                // answerable from the logs without reading configuration.
                logger.LogInformation(
                    "Route estimates will use the sandbox estimator: Google Maps routing is {State}.",
                    googleMapsOptions.Enabled ? "missing an API key" : "disabled by configuration");

                return serviceProvider.GetRequiredService<SandboxRouteEstimateProvider>();
            }

            logger.LogInformation("Route estimates will use the Google Maps Routes API.");
            return ActivatorUtilities.CreateInstance<GoogleMapsRouteEstimateProvider>(serviceProvider);
        });

        return services;
    }
}
