using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nestly.Application.Routing;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Builds task 289's travel-feasibility check for tests that construct
/// services directly (no DI container). Shared because
/// <see cref="ProviderAssignmentEligibilityService"/> now needs one and three
/// separate suites build that service for reasons of their own.
/// </summary>
public static class TravelFeasibilityFactory
{
    /// <summary>
    /// The check backed by the real <see cref="SandboxRouteEstimateProvider"/> -
    /// deterministic, and incapable of touching the network or needing a key.
    /// Suites whose bookings all share one address get zero-length legs and so
    /// never see the check fire at all, which is what keeps their pre-289
    /// expectations intact.
    /// </summary>
    public static ProviderTravelFeasibilityService Sandbox(NestlyDbContext context, AutoAssignmentOptions? options = null)
    {
        var sandbox = new SandboxRouteEstimateProvider(Options.Create(new SandboxRouteEstimateOptions()));
        return Build(context, sandbox, sandbox, options);
    }

    /// <summary>The check driven by a stubbed router, for the suite that pins the travel rule itself.</summary>
    public static ProviderTravelFeasibilityService Build(
        NestlyDbContext context,
        IRouteEstimateProvider routeEstimateProvider,
        SandboxRouteEstimateProvider sandboxRouteEstimateProvider,
        AutoAssignmentOptions? options = null) => new(
            context,
            routeEstimateProvider,
            sandboxRouteEstimateProvider,
            Options.Create(options ?? new AutoAssignmentOptions()),
            NullLogger<ProviderTravelFeasibilityService>.Instance);
}
