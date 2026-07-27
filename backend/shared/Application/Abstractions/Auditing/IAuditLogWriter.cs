namespace Nestly.Application.Abstractions.Auditing;

/// <summary>
/// Records critical business and admin actions to the audit trail (T020,
/// SRS 21; docs/DEVOPS.md — "Audit logs for critical business and admin
/// actions").
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>
    /// Appends <paramref name="entry"/> to the audit trail, attributed to the
    /// current <see cref="AuditContext"/>.
    /// </summary>
    /// <remarks>
    /// The entry is added to the current unit of work and persisted by the
    /// caller's <c>SaveChangesAsync</c>. This is deliberate: an audit row must
    /// commit in the same transaction as the change it describes, so a rolled
    /// back business operation cannot leave a phantom audit entry behind — and
    /// a committed one is never missing its record.
    /// </remarks>
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
