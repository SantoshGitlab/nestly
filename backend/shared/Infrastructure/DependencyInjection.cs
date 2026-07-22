using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nestly.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure services: persistence, caching, background jobs,
    /// and external providers. Wiring is added as each capability lands
    /// (EF Core/PostgreSQL in T008, Redis in T017, Hangfire in T018).
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration;
        return services;
    }
}
