using Nestly.Domain;

namespace Nestly.Application.Auditing;

/// <summary>
/// Coarse result of an audited action, for the audit-log-viewer filter (task
/// 130, SRS 21). <see cref="AuditLog"/> has no dedicated outcome column — the
/// two current writers (<c>AdminLoginService</c>, task 95g;
/// <c>PermissionAuthorizationHandler</c>, task 96d) already encode the
/// outcome inside <see cref="AuditLog.Action"/> itself (e.g.
/// "PermissionDenied:catalog.write", "AdminLoginFailed"). This enum is the
/// filterable projection of that convention — see
/// <see cref="AuditOutcomeClassifier"/> for the derivation rule both the
/// filter and the response mapping share.
/// </summary>
public enum AuditOutcome
{
    /// <summary>A permission check succeeded and the underlying action proceeded.</summary>
    Grant,

    /// <summary>A permission check failed and the underlying action was refused.</summary>
    Deny,

    /// <summary>A non-permission action (e.g. login) completed as intended.</summary>
    Success,

    /// <summary>A non-permission action (e.g. login) did not complete as intended.</summary>
    Failure
}

/// <summary>
/// Audit trail search filter (task 130). Every field is optional; an absent
/// field imposes no constraint. Covers the SRS-21/task-130 minimum: actor,
/// date range, module/action (via <see cref="EntityName"/>/<see cref="Action"/>
/// — <see cref="AuditLog"/> has no separate "module" column, and
/// <see cref="AuditLog.EntityName"/> is the closest thing to one that
/// actually exists on the entity), and outcome.
/// </summary>
/// <param name="ActorType">Kind of principal that performed the action.</param>
/// <param name="ActorId">Exact acting principal.</param>
/// <param name="EntityName">Exact logical entity name audited against, e.g. "AdminUser".</param>
/// <param name="Action">Substring match against the recorded action, e.g. "Login".</param>
/// <param name="Outcome">Coarse result, derived per-row by <see cref="AuditOutcomeClassifier"/>.</param>
/// <param name="FromUtc">Inclusive lower bound on <see cref="AuditLog.OccurredOnUtc"/>.</param>
/// <param name="ToUtc">Inclusive upper bound on <see cref="AuditLog.OccurredOnUtc"/>.</param>
/// <param name="Page">1-based page number; values below 1 are treated as 1.</param>
/// <param name="PageSize">Rows per page; clamped to (0, 100].</param>
public sealed record AuditLogFilterRequest(
    AuditActorType? ActorType = null,
    Guid? ActorId = null,
    string? EntityName = null,
    string? Action = null,
    AuditOutcome? Outcome = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 25);

/// <summary>One audit trail row, as shown on the audit-log-viewer screen.</summary>
public sealed record AuditLogEntryResponse(
    Guid Id,
    AuditActorType ActorType,
    Guid? ActorId,
    string EntityName,
    string EntityId,
    string Action,
    AuditOutcome Outcome,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    string? CorrelationId,
    DateTime OccurredOnUtc);

/// <summary>A page of audit trail rows, newest first, with the total match count for pagination.</summary>
public sealed record PagedAuditLogResponse(
    IReadOnlyList<AuditLogEntryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// Derives the coarse <see cref="AuditOutcome"/> of an audit row from its
/// <see cref="AuditLog.Action"/> text — the only place that outcome is
/// currently recorded. Precedence matters where it is checked in the query
/// filter too (<c>AuditLogQueryService</c>): Deny before Grant before Failure
/// before the Success default, so a hypothetical future action name
/// containing more than one marker classifies the same way in both places.
/// </summary>
public static class AuditOutcomeClassifier
{
    /// <summary>Substring <c>PermissionAuthorizationHandler</c> embeds for a refused check.</summary>
    public const string DeniedMarker = "Denied";

    /// <summary>Substring <c>PermissionAuthorizationHandler</c> embeds for an allowed, audited check.</summary>
    public const string GrantedMarker = "Granted";

    /// <summary>Substring <c>AdminLoginService</c> embeds for an unsuccessful attempt.</summary>
    public const string FailedMarker = "Failed";

    public static AuditOutcome Classify(string action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (action.Contains(DeniedMarker, StringComparison.Ordinal))
        {
            return AuditOutcome.Deny;
        }

        if (action.Contains(GrantedMarker, StringComparison.Ordinal))
        {
            return AuditOutcome.Grant;
        }

        if (action.Contains(FailedMarker, StringComparison.Ordinal))
        {
            return AuditOutcome.Failure;
        }

        return AuditOutcome.Success;
    }
}
