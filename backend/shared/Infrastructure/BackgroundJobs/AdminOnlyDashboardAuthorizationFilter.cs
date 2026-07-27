using Hangfire.Dashboard;
using Nestly.Infrastructure.Auditing;

namespace Nestly.Infrastructure.BackgroundJobs;

/// <summary>
/// Restricts the Hangfire dashboard to authenticated administrators (T018).
/// </summary>
/// <remarks>
/// Deny-by-default: the dashboard can enqueue, requeue and delete jobs, so
/// anything short of a positive admin-role match is refused. Until JWT issuance
/// lands (T025) no principal carries the admin role, so this correctly denies
/// everyone rather than silently exposing the dashboard in the meantime.
/// </remarks>
public sealed class AdminOnlyDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        return httpContext.User?.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole(HttpAuditContextProvider.AdminRole);
    }
}
