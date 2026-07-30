using MediatR;
using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Wraps a framework-free <see cref="IDomainEvent"/> so it can be published
/// through MediatR without the Domain layer taking a dependency on it.
/// </summary>
public sealed class DomainEventNotification<TDomainEvent>(TDomainEvent domainEvent) : INotification
    where TDomainEvent : IDomainEvent
{
    public TDomainEvent DomainEvent { get; } = domainEvent;
}
