namespace Nestly.BuildingBlocks.Primitives;

/// <summary>
/// Base record for domain events with identity and timestamp assigned at creation.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
