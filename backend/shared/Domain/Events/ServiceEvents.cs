using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

public sealed record ServiceCreatedEvent(Guid ServiceId, Guid CategoryId) : DomainEvent;

public sealed record ServiceActivatedEvent(Guid ServiceId) : DomainEvent;

public sealed record ServiceDeactivatedEvent(Guid ServiceId) : DomainEvent;

public sealed record ServicePriceChangedEvent(Guid ServiceId, decimal OldPrice, decimal NewPrice) : DomainEvent;
