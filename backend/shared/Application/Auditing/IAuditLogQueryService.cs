using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Auditing;

/// <summary>
/// Read side of the audit trail (task 130, SRS 21): the filterable search
/// behind the admin audit-log-viewer screen. Reads the same <c>audit_log</c>
/// table every writer already appends to (<see cref="IAuditLogWriter"/>
/// implementations, e.g. task 95g's login audit, task 96d's permission-check
/// audit) — there is deliberately no second audit table or projection here.
/// </summary>
public interface IAuditLogQueryService
{
    /// <summary>
    /// Searches the audit trail, newest first, applying every constraint set
    /// on <paramref name="filter"/>. Fails validation only when the supplied
    /// date range is inverted (<c>FromUtc</c> after <c>ToUtc</c>).
    /// </summary>
    Task<Result<PagedAuditLogResponse>> SearchAsync(
        AuditLogFilterRequest filter, CancellationToken cancellationToken = default);
}
