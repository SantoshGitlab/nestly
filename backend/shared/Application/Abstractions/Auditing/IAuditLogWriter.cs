namespace Nestly.Application.Abstractions.Auditing;

/// <summary>
/// Records critical business and admin actions to the audit trail (T020,
/// SRS 21; docs/DEVOPS.md — "Audit logs for critical business and admin
/// actions").
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>
    /// Appends <paramref name="entry"/> to the audit trail, attributed to
    /// <paramref name="context"/> when supplied, or to the current ambient
    /// <see cref="IAuditContextProvider"/> attribution otherwise.
    /// </summary>
    /// <param name="context">
    /// Explicit attribution override for the rare caller that must not use
    /// whoever happens to be the ambient HTTP/job principal - e.g. an
    /// automatic side effect of a human's request (auto-enabling/-disabling a
    /// <c>ServicePincodeMapping</c> as a consequence of a provider or admin
    /// action elsewhere) is attributed to <see cref="AuditContext.System"/>
    /// rather than to that human, since the toggle itself is a system
    /// decision, not theirs. Omit to keep the ordinary ambient-context
    /// behaviour every other caller uses.
    /// </param>
    /// <remarks>
    /// The entry is added to the current unit of work and persisted by the
    /// caller's <c>SaveChangesAsync</c>. This is deliberate: an audit row must
    /// commit in the same transaction as the change it describes, so a rolled
    /// back business operation cannot leave a phantom audit entry behind — and
    /// a committed one is never missing its record.
    /// </remarks>
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default, AuditContext? context = null);
}
