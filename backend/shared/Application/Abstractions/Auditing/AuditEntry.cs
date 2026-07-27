namespace Nestly.Application.Abstractions.Auditing;

/// <summary>
/// Describes <em>what</em> happened in an audited action (T020 / SRS 21). The
/// <em>who</em> and <em>where</em> — actor, IP, correlation id — are supplied by
/// <see cref="IAuditContextProvider"/> rather than by the caller, so business
/// code cannot accidentally attribute an action to the wrong principal.
/// </summary>
/// <param name="EntityName">Logical entity name, e.g. "Booking". Not a table name.</param>
/// <param name="EntityId">Key of the affected row, as text.</param>
/// <param name="Action">Action performed, e.g. "Created", "Cancelled".</param>
/// <param name="OldValues">
/// JSON snapshot of the changed fields before the action; null on create.
/// Must already have secrets and PII stripped by the caller.
/// </param>
/// <param name="NewValues">
/// JSON snapshot of the changed fields after the action; null on delete.
/// Must already have secrets and PII stripped by the caller.
/// </param>
public sealed record AuditEntry(
    string EntityName,
    string EntityId,
    string Action,
    string? OldValues = null,
    string? NewValues = null);
