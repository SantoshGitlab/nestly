namespace Nestly.BuildingBlocks.Primitives;

/// <summary>
/// A fact that happened in the domain. Raised by aggregates and dispatched
/// after the owning transaction commits.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}
