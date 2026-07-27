using Nestly.Domain;

namespace Nestly.Application.Abstractions.Auditing;

/// <summary>
/// Ambient attribution for an audited action (T020): who is acting, from where,
/// under which correlation id. Implemented over the current request in the API
/// process, and as a fixed system principal inside background jobs.
/// </summary>
public interface IAuditContextProvider
{
    AuditContext GetCurrent();
}

/// <summary>
/// Resolved attribution for the action currently being performed.
/// </summary>
/// <param name="ActorType">Kind of principal responsible for the action.</param>
/// <param name="ActorId">Identifier of the principal; null when anonymous or system.</param>
/// <param name="IpAddress">Origin IP, when the action came from a request.</param>
/// <param name="CorrelationId">Correlation id tying this action to the request logs.</param>
public sealed record AuditContext(
    AuditActorType ActorType,
    Guid? ActorId,
    string? IpAddress,
    string? CorrelationId)
{
    /// <summary>Attribution for work with no human actor, e.g. a scheduled job.</summary>
    public static AuditContext System { get; } =
        new(AuditActorType.System, ActorId: null, IpAddress: null, CorrelationId: null);
}
