using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

public sealed record CategoryCreatedEvent(Guid CategoryId) : DomainEvent;

public sealed record CategoryActivatedEvent(Guid CategoryId) : DomainEvent;

public sealed record CategoryDeactivatedEvent(Guid CategoryId) : DomainEvent;
