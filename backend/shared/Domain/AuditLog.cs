using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>Who performed an audited action.</summary>
public enum AuditActorType
{
    /// <summary>An unauthenticated or not-yet-identified caller.</summary>
    Anonymous,

    /// <summary>An end customer acting on their own data.</summary>
    Customer,

    /// <summary>A back-office operator acting through the admin API.</summary>
    AdminUser,

    /// <summary>A background job or scheduled process with no human actor.</summary>
    System
}

/// <summary>
/// An immutable record of a critical business or admin action (SRS 21, SRS 32.2;
/// docs/DEVOPS.md — "Audit logs for critical business and admin actions").
/// </summary>
/// <remarks>
/// Append-only by construction: there are no mutators, and the persistence
/// configuration grants no update path. An audit trail that can be edited after
/// the fact is not an audit trail.
/// <para>
/// <see cref="OldValues"/> and <see cref="NewValues"/> hold JSON snapshots of
/// the changed fields only. Callers are responsible for excluding secrets and
/// PII before constructing the entry — this type cannot know which properties
/// of an arbitrary entity are sensitive, and the project rule is absolute:
/// never log passwords, tokens, or PII.
/// </para>
/// </remarks>
public class AuditLog : Entity<Guid>
{
    public AuditActorType ActorType { get; private set; }

    /// <summary>Identifier of the acting principal; null for anonymous/system actions.</summary>
    public Guid? ActorId { get; private set; }

    /// <summary>Logical entity name, e.g. "Booking". Not a table name.</summary>
    public string EntityName { get; private set; } = string.Empty;

    /// <summary>Key of the affected row, as text so any key type is representable.</summary>
    public string EntityId { get; private set; } = string.Empty;

    /// <summary>Action performed, e.g. "Created", "Cancelled", "StatusChanged".</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>JSON snapshot of changed fields before the action; null on create.</summary>
    public string? OldValues { get; private set; }

    /// <summary>JSON snapshot of changed fields after the action; null on delete.</summary>
    public string? NewValues { get; private set; }

    /// <summary>Origin IP of the request, when it came from one.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Correlation id of the originating request, tying this entry to the request logs.</summary>
    public string? CorrelationId { get; private set; }

    public DateTime OccurredOnUtc { get; private set; }

    protected AuditLog() { }

    public AuditLog(
        Guid id,
        AuditActorType actorType,
        Guid? actorId,
        string entityName,
        string entityId,
        string action,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        string? correlationId = null,
        DateTime? occurredOnUtc = null)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        ActorType = actorType;
        ActorId = actorId;
        EntityName = entityName;
        EntityId = entityId;
        Action = action;
        OldValues = oldValues;
        NewValues = newValues;
        IpAddress = ipAddress;
        CorrelationId = correlationId;
        OccurredOnUtc = occurredOnUtc ?? DateTime.UtcNow;
    }
}
