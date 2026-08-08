namespace Nestly.BuildingBlocks.Primitives;

/// <summary>
/// Base record for domain events with identity and timestamp assigned at creation.
/// </summary>
/// <remarks>
/// <b>Both members are <c>init</c> rather than get-only, and that is load
/// bearing (task 294).</b> A notification intent persists the event that
/// warranted it and the sweep deserializes it back to re-run the same handler.
/// The deduplication key is built from <see cref="EventId"/>, so if the
/// round trip minted a fresh id - which is exactly what a get-only property
/// with an initializer does under <c>System.Text.Json</c> - the retry path
/// would compute a different key from the in-process path and the two would
/// stop deduplicating each other. Adding an <c>init</c> accessor is what makes
/// an event's identity survive being written down. Nothing should ever set
/// either member explicitly outside deserialization.
/// </remarks>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
