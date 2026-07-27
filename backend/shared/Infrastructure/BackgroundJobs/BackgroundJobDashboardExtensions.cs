using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.BackgroundJobs;

/// <summary>
/// Pipeline wiring for the Hangfire dashboard (T018).
/// </summary>
public static class BackgroundJobDashboardExtensions
{
    /// <summary>
    /// Mounts the Hangfire dashboard when
    /// <see cref="BackgroundJobOptions.DashboardEnabled"/> is set, restricted to
    /// administrators.
    /// </summary>
    /// <remarks>
    /// Call only from the admin API, and only after authentication and
    /// authorization middleware — the dashboard can enqueue and delete jobs, so
    /// the authorization filter needs a populated
    /// <c>HttpContext.User</c> to check against.
    /// </remarks>
    public static IApplicationBuilder UseBackgroundJobsDashboard(this IApplicationBuilder app)
    {
        BackgroundJobOptions options = app.ApplicationServices
            .GetRequiredService<IOptions<BackgroundJobOptions>>().Value;

        if (!options.DashboardEnabled)
        {
            return app;
        }

        app.UseHangfireDashboard(options.DashboardPath, new DashboardOptions
        {
            Authorization = [new AdminOnlyDashboardAuthorizationFilter()],

            // Hangfire defaults this to true, which renders the storage
            // connection string — including the database password — into the
            // dashboard UI. Never enable it.
            DisplayStorageConnectionString = false,
        });

        return app;
    }
}
