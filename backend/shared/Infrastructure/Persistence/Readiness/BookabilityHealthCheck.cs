using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nestly.Infrastructure.Persistence.Readiness;

/// <summary>
/// Exposes <see cref="BookabilityProbe"/> on the health endpoints (task 389).
/// Registered in <c>AddInfrastructure</c> with
/// <see cref="HealthStatus.Degraded"/> as its failure status and the tags
/// <c>ready</c> and <c>bootstrap</c>.
///
/// <para>
/// <b>Degraded, never Unhealthy - on purpose.</b> An empty database is the
/// legitimate state of every deployment between <c>database update</c> and the
/// operator's first bootstrap run, so "nothing is bookable" is a fact about
/// the data, not a fault in the process. Reporting it as Unhealthy would take
/// <c>/health/ready</c> to 503, which in any orchestrator means the pods are
/// held out of rotation - including the admin API the operator has to reach in
/// order to seed the very rows being complained about. That is a deadlock, and
/// it converts a data gap into an outage. Degraded keeps
/// <c>/health/ready</c> on HTTP 200 (the framework's default status mapping)
/// while flipping its body from <c>Healthy</c> to <c>Degraded</c>, and the
/// per-gap detail is on <c>/health/bootstrap</c>.
/// </para>
///
/// <para>
/// Exceptions are not caught here. A thrown check resolves to the registered
/// failure status, which is Degraded, so a database that is unreachable or not
/// yet migrated degrades this check rather than failing it - and the
/// <c>postgres</c> check registered alongside it is the one that should report
/// an unreachable database, not this one.
/// </para>
/// </summary>
public sealed class BookabilityHealthCheck : IHealthCheck
{
    /// <summary>The name this check is registered under, and the key it appears as in the health payload.</summary>
    public const string Name = "bookability";

    private readonly BookabilityProbe _probe;

    public BookabilityHealthCheck(BookabilityProbe probe)
    {
        _probe = probe;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var report = await _probe.InspectAsync(cancellationToken);

        if (report.IsReady)
        {
            return HealthCheckResult.Healthy(report.Describe());
        }

        // Keyed by gap code so an alert rule can match a specific missing link
        // rather than string-matching a sentence that may be reworded.
        var data = report.Gaps.ToDictionary(
            gap => gap.Code,
            gap => (object)gap.Remedy);

        return new HealthCheckResult(
            context.Registration.FailureStatus,
            report.Describe(),
            data: data);
    }
}
