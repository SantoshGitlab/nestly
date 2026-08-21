using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Nestly.Infrastructure.Persistence.Readiness;

/// <summary>
/// Wires <see cref="BookabilityProbe"/> into a host: one line in the startup
/// log, and one endpoint an operator or an alert can pull (task 389).
/// Shaped as <see cref="IApplicationBuilder"/> / <see cref="IEndpointRouteBuilder"/>
/// extensions called from <c>Program.cs</c> for the same reason
/// <c>AdminPermissionReconciliationExtensions.ReconcileAdminPermissions</c>
/// is: the work needs a built <see cref="IServiceProvider"/> to resolve a
/// scoped <c>NestlyDbContext</c> from.
/// </summary>
public static class BookabilityReadinessExtensions
{
    /// <summary>The tag <see cref="MapBookabilityHealthCheck"/> filters the health registry on.</summary>
    public const string BootstrapTag = "bootstrap";

    /// <summary>Route serving the per-gap detail. Sits beside <c>/health/live</c> and <c>/health/ready</c>.</summary>
    public const string BootstrapHealthPath = "/health/bootstrap";

    /// <summary>
    /// Logs, once per process start, whether this database can serve a
    /// booking at all - a warning naming every missing link when it cannot,
    /// and a single information line when it can.
    ///
    /// <para>
    /// <b>Why a log and not a startup failure.</b> The state being reported is
    /// the correct state of a deployment that has been migrated and not yet
    /// seeded. Refusing to start would mean the operator cannot reach the
    /// admin API to seed it, so the check would be preventing its own remedy;
    /// see <see cref="BookabilityHealthCheck"/> for the same call made on the
    /// health endpoint. Warning rather than Error for the same reason: nothing
    /// has malfunctioned. What matters is that the line exists at all - the
    /// bug this closes produced no output of any kind.
    /// </para>
    ///
    /// <para>
    /// Called from all three API hosts. The condition is a property of the
    /// database rather than of the process, so each host reports the same
    /// verdict, and an operator reading whichever log they happened to open
    /// gets it. It writes nothing, so unlike
    /// <c>ReconcileAdminPermissions</c> there is no cold-start race to avoid
    /// by nominating one host.
    /// </para>
    ///
    /// <para>
    /// A failure here is logged and swallowed. This is a diagnostic; taking a
    /// host down because its diagnostic could not run (for instance when the
    /// process starts before migrations are applied) would turn an empty
    /// database into an outage.
    /// </para>
    /// </summary>
    public static IApplicationBuilder ReportBookabilityReadiness(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<BookabilityProbe>>();

        try
        {
            var report = scope.ServiceProvider
                .GetRequiredService<BookabilityProbe>()
                .InspectAsync()
                .GetAwaiter()
                .GetResult();

            if (report.IsReady)
            {
                logger.LogInformation("{BookabilityStatus}", report.Describe());
                return app;
            }

            logger.LogWarning(
                "{BookabilityStatus} Run database/seed/bootstrap-launch-city.sql (or configure geography, catalog and slot windows through the admin panel), then check {BootstrapHealthPath}. Missing links: {BookabilityGapCodes}.",
                report.Describe(),
                BootstrapHealthPath,
                string.Join(", ", report.Gaps.Select(gap => gap.Code)));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "The bookability readiness check could not run; whether this database can serve a booking is unknown. Pull {BootstrapHealthPath} once the database is reachable.",
                BootstrapHealthPath);
        }

        return app;
    }

    /// <summary>
    /// Maps <see cref="BootstrapHealthPath"/>, which reports only the checks
    /// tagged <see cref="BootstrapTag"/> and, unlike the default plain-text
    /// writer used by <c>/health/live</c> and <c>/health/ready</c>, renders
    /// each check's description and data. That detail is the point: "Degraded"
    /// on its own tells an operator no more than an empty slot list does.
    ///
    /// <para>
    /// Degraded is mapped to 200 here, matching the framework default that
    /// <c>/health/ready</c> already relies on - an unbootstrapped database
    /// must not read as a failed probe (see
    /// <see cref="BookabilityHealthCheck"/>).
    /// </para>
    /// </summary>
    public static IEndpointConventionBuilder MapBookabilityHealthCheck(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHealthChecks(BootstrapHealthPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(BootstrapTag),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },
            ResponseWriter = WriteBootstrapReportAsync,
        });

    private static Task WriteBootstrapReportAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                // Gap code -> remedy, straight from BookabilityHealthCheck.
                gaps = entry.Value.Data.ToDictionary(item => item.Key, item => item.Value?.ToString()),
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
