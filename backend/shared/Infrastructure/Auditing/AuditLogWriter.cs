using System.Text.Json;
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
            oldValues: AsJson(entry.OldValues),
            newValues: AsJson(entry.NewValues),
            ipAddress: context.IpAddress,
            correlationId: context.CorrelationId);

        // Added to the current unit of work only — the caller's SaveChangesAsync
        // commits it in the same transaction as the change it describes.
        await _context.Set<AuditLog>().AddAsync(auditLog, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <see cref="AuditLog.OldValues"/>/<see cref="AuditLog.NewValues"/> are stored as
    /// <c>jsonb</c> (see AuditLogConfiguration), but several callers build these strings
    /// with plain interpolation (e.g. <c>$"ProviderId={id}; Status={old}->{new}"</c>) rather
    /// than <c>JsonSerializer.Serialize</c>, since not every audited change has a rich object
    /// to serialize. Postgres rejects non-JSON text for a jsonb column outright (22P02), which
    /// used to surface as a 500 on every such write. Rather than hunting down and rewriting each
    /// ad hoc call site, this normalizes at the one place all of them funnel through: valid JSON
    /// passes through untouched, anything else is wrapped as a JSON string so it's always
    /// well-formed and still human-readable in the audit screen.
    /// </summary>
    private static string? AsJson(string? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
            return value;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(value);
        }
    }
}
