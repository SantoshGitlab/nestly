using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Nestly.Infrastructure.Persistence.Seed;

/// <summary>
/// Runs <see cref="AdminPermissionReconciler"/> once during startup. Shaped as
/// an <see cref="IApplicationBuilder"/> extension called from
/// <c>Program.cs</c> for the same reason
/// <c>RecurringBookingJobScheduleExtensions.ScheduleRecurringBookingJob</c>
/// is: the work needs a built <see cref="IServiceProvider"/> to resolve a
/// scoped <see cref="NestlyDbContext"/> from.
/// </summary>
public static class AdminPermissionReconciliationExtensions
{
    /// <summary>
    /// Call only from admin-api: it is the sole process that serves the admin
    /// RBAC surface, and one reconciling process avoids two APIs racing to
    /// insert the same permission rows on a cold start.
    ///
    /// <para>
    /// A failure here is logged and swallowed rather than allowed to abort
    /// startup. The reconciler is a data-completeness safeguard, not a
    /// serving dependency - taking the whole admin API down over it (for
    /// instance when the process starts before migrations have been applied)
    /// would turn a missing-permission-row inconvenience into an outage,
    /// and the next restart retries anyway.
    /// </para>
    /// </summary>
    public static IApplicationBuilder ReconcileAdminPermissions(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AdminPermissionReconciler>>();

        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NestlyDbContext>();
            new AdminPermissionReconciler(dbContext, logger)
                .ReconcileAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Admin permission reconciliation failed; modules added since the last seed migration may be inaccessible until it succeeds.");
        }

        return app;
    }
}
