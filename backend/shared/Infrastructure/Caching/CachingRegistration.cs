using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nestly.Application.Abstractions.Caching;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Caching;

/// <summary>
/// Distributed cache registration (T017).
/// </summary>
internal static class CachingRegistration
{
    /// <summary>
    /// Registers <see cref="ICacheService"/> over Redis when a connection
    /// string is configured, and over an in-process store otherwise.
    /// </summary>
    /// <remarks>
    /// The fallback keeps local development and integration tests running
    /// without a Redis container while exercising the same
    /// <see cref="ICacheService"/> code path. It is not suitable for a scaled
    /// deployment — each replica would cache independently — which is why
    /// deployed environments configure <c>Cache:ConnectionString</c>.
    /// </remarks>
    internal static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<CacheOptions>()
            .Bind(configuration.GetSection(CacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var cacheOptions = new CacheOptions();
        configuration.GetSection(CacheOptions.SectionName).Bind(cacheOptions);

        if (cacheOptions.IsRedisConfigured)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = cacheOptions.ConnectionString;
                options.InstanceName = cacheOptions.InstanceName;
            });

            services
                .AddHealthChecks()
                .AddRedis(cacheOptions.ConnectionString!, name: "redis", tags: ["ready"]);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<ICacheService, DistributedCacheService>();

        return services;
    }
}
