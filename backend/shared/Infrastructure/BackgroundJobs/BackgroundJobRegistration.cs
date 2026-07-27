using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire server and client registration (T018).
/// </summary>
internal static class BackgroundJobRegistration
{
    /// <summary>
    /// Registers Hangfire against the application's PostgreSQL database, with
    /// the project's retry conventions applied globally.
    /// </summary>
    /// <remarks>
    /// Hangfire is hosted inside the API process (docs/DEVOPS.md) and shares
    /// the application database rather than taking a second datastore
    /// dependency; it provisions its own schema on first run.
    /// </remarks>
    internal static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services
            .AddOptions<BackgroundJobOptions>()
            .Bind(configuration.GetSection(BackgroundJobOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jobOptions = new BackgroundJobOptions();
        configuration.GetSection(BackgroundJobOptions.SectionName).Bind(jobOptions);

        services.AddHangfire((_, config) =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(connectionString))
                // Retry convention: bounded attempts with a widening backoff, so
                // a transient dependency failure recovers on its own while a
                // genuinely broken job stops rather than retrying forever.
                // Jobs must be idempotent — a retry re-runs the whole method
                // (docs/DEVOPS.md: "safe re-execution").
                .UseFilter(new AutomaticRetryAttribute
                {
                    Attempts = jobOptions.RetryAttempts,
                    DelaysInSeconds = [10, 60, 300, 900, 3600],
                    OnAttemptsExceeded = AttemptsExceededAction.Fail,
                })
                // Prevents a second instance of the same job overlapping a slow
                // run — required for the "safe re-execution" guarantee above.
                .UseFilter(new DisableConcurrentExecutionAttribute(timeoutInSeconds: 300));
        });

        if (jobOptions.ServerEnabled)
        {
            services.AddHangfireServer(serverOptions =>
            {
                if (jobOptions.WorkerCount is { } workerCount)
                {
                    serverOptions.WorkerCount = workerCount;
                }

                // Gives in-flight jobs a window to observe their cancellation
                // token and stop cleanly on shutdown, rather than being killed
                // mid-transaction (docs/DEVOPS.md: graceful shutdown).
                serverOptions.ShutdownTimeout = TimeSpan.FromSeconds(30);
            });
        }

        return services;
    }
}
