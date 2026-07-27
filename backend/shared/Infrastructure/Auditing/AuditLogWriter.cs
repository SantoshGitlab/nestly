using Nestly.Application.Abstractions.Auditing;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Auditing;

/// <summary>
/// Persists audit entries to the <c>audit_log</c> table (T020).
/// </summary>
public sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly NestlyDbContext _context;
    private readonly IAuditContextProvider _auditContextProvider;

    public AuditLogWriter(NestlyDbContext context, IAuditContextProvider auditContextProvider)
    {
        _context = context;
        _auditContextProvider = auditContextProvider;
    }

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        AuditContext context = _auditContextProvider.GetCurrent();

        var auditLog = new AuditLog(
            id: Guid.NewGuid(),
            actorType: context.ActorType,
            actorId: context.ActorId,
            entityName: entry.EntityName,
            entityId: entry.EntityId,
            action: entry.Action,
            oldValues: entry.OldValues,
            newValues: entry.NewValues,
            ipAddress: context.IpAddress,
            correlationId: context.CorrelationId);

        // Added to the current unit of work only — the caller's SaveChangesAsync
        // commits it in the same transaction as the change it describes.
        await _context.Set<AuditLog>().AddAsync(auditLog, cancellationToken).ConfigureAwait(false);
    }
}
