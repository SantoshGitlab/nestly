using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

public sealed record ServiceAddOnCreatedEvent(Guid ServiceAddOnId, Guid ServiceId) : DomainEvent;

public sealed record ServiceAddOnActivatedEvent(Guid ServiceAddOnId) : DomainEvent;

public sealed record ServiceAddOnDeactivatedEvent(Guid ServiceAddOnId) : DomainEvent;
