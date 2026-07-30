using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

public sealed record BookingCreatedEvent(Guid BookingId, Guid CustomerId) : DomainEvent;

public sealed record BookingStatusChangedEvent(Guid BookingId, BookingStatus FromStatus, BookingStatus ToStatus) : DomainEvent;
