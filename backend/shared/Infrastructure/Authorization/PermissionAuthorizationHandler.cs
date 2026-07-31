using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Identity;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Authorization;

/// <summary>
/// Evaluates a <see cref="PermissionRequirement"/> against the caller's
/// <see cref="AdminClaimTypes.Permission"/> claims — the claims
/// <c>AdminTokenService</c> embedded at login from the account's role
/// (task 96c) — and audits the decision (task 96d, SECURITY.md's "Permission
/// failures" / "Administrative actions" logging requirement).
/// </summary>
/// <remarks>
/// Every denial is audited: it is both rare (a legitimate client should
/// rarely hit a policy it isn't granted) and security-relevant. Grants are
/// only audited for Write permissions — a Read-permission grant fires on
/// every routine GET and would drown the audit trail in noise
/// (CODING-STANDARDS.md "Avoid excessive logging"); a Write grant means a
/// mutating admin action is actually about to happen, which is the
/// "sensitive-action grant" task 96d asks to capture.
/// </remarks>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private const string EntityName = "AdminPermissionCheck";
    private const string UnknownActor = "unknown";

    private readonly IAuditLogWriter _auditLogWriter;
    private readonly NestlyDbContext _dbContext;

    public PermissionAuthorizationHandler(IAuditLogWriter auditLogWriter, NestlyDbContext dbContext)
    {
        _auditLogWriter = auditLogWriter;
        _dbContext = dbContext;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        string actorId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? UnknownActor;
        bool isGranted = context.User.HasClaim(AdminClaimTypes.Permission, requirement.PermissionCode);

        if (isGranted)
        {
            context.Succeed(requirement);

            bool isSensitiveWriteAction = requirement.PermissionCode.EndsWith(".write", StringComparison.Ordinal);
            if (isSensitiveWriteAction)
            {
                await RecordAsync(actorId, $"PermissionGranted:{requirement.PermissionCode}");
            }

            return;
        }

        // Deliberately not context.Fail(): leaving the requirement merely
        // unmet (rather than forcing the whole AuthorizationHandlerContext to
        // fail) means an unrelated requirement evaluated in the same policy
        // is unaffected by this one being denied - ASP.NET Core already
        // rejects the request once any requirement goes unmet.
        await RecordAsync(actorId, $"PermissionDenied:{requirement.PermissionCode}");
    }

    private async Task RecordAsync(string actorId, string action)
    {
        await _auditLogWriter.WriteAsync(new AuditEntry(EntityName, actorId, action));
        await _dbContext.SaveChangesAsync();
    }
}
