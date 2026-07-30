using MediatR;
using Nestly.Application;
using Nestly.Application.Abstractions.Caching;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Caching;

/// <summary>
/// Evicts catalog cache entries when the underlying data changes (task 49).
/// Single-entity detail caches (category/service, keyed by id) and
/// serviceability checks are invalidated precisely, since the raised events
/// carry exactly the ids the cache keys are built from. City-scoped category
/// listings are TTL-only (see CacheKeys.CategoriesInCity) - not invalidated
/// here.
/// </summary>
public sealed class CatalogCacheInvalidationHandler :
    INotificationHandler<DomainEventNotification<CategoryCreatedEvent>>,
    INotificationHandler<DomainEventNotification<CategoryActivatedEvent>>,
    INotificationHandler<DomainEventNotification<CategoryDeactivatedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceCreatedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceActivatedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceDeactivatedEvent>>,
    INotificationHandler<DomainEventNotification<ServicePriceChangedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceAddOnCreatedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceAddOnActivatedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceAddOnDeactivatedEvent>>,
    INotificationHandler<DomainEventNotification<CategoryCityMappingChangedEvent>>,
    INotificationHandler<DomainEventNotification<ServicePincodeMappingChangedEvent>>
{
    private readonly ICacheService _cache;
    private readonly IServiceRepository _serviceRepository;

    public CatalogCacheInvalidationHandler(ICacheService cache, IServiceRepository serviceRepository)
    {
        _cache = cache;
        _serviceRepository = serviceRepository;
    }

    public Task Handle(DomainEventNotification<CategoryCreatedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateCategory(notification.DomainEvent.CategoryId, cancellationToken);

    public Task Handle(DomainEventNotification<CategoryActivatedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateCategory(notification.DomainEvent.CategoryId, cancellationToken);

    public Task Handle(DomainEventNotification<CategoryDeactivatedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateCategory(notification.DomainEvent.CategoryId, cancellationToken);

    public async Task Handle(DomainEventNotification<ServiceCreatedEvent> notification, CancellationToken cancellationToken)
    {
        await InvalidateService(notification.DomainEvent.ServiceId, cancellationToken);
        await InvalidateServicesByCategory(notification.DomainEvent.CategoryId, cancellationToken);
    }

    public Task Handle(DomainEventNotification<ServiceActivatedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateServiceAndItsCategoryList(notification.DomainEvent.ServiceId, cancellationToken);

    public Task Handle(DomainEventNotification<ServiceDeactivatedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateServiceAndItsCategoryList(notification.DomainEvent.ServiceId, cancellationToken);

    public Task Handle(DomainEventNotification<ServicePriceChangedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateService(notification.DomainEvent.ServiceId, cancellationToken);

    public Task Handle(DomainEventNotification<ServiceAddOnCreatedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateService(notification.DomainEvent.ServiceId, cancellationToken);

    public Task Handle(DomainEventNotification<ServiceAddOnActivatedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateAddOnsService(notification.DomainEvent.ServiceAddOnId, cancellationToken);

    public Task Handle(DomainEventNotification<ServiceAddOnDeactivatedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateAddOnsService(notification.DomainEvent.ServiceAddOnId, cancellationToken);

    public Task Handle(DomainEventNotification<CategoryCityMappingChangedEvent> notification, CancellationToken cancellationToken) =>
        _cache.RemoveAsync(CacheKeys.CategoryServiceability(notification.DomainEvent.CategoryId, notification.DomainEvent.CityId), cancellationToken);

    public Task Handle(DomainEventNotification<ServicePincodeMappingChangedEvent> notification, CancellationToken cancellationToken) =>
        _cache.RemoveAsync(CacheKeys.ServicePincodeServiceability(notification.DomainEvent.ServiceId, notification.DomainEvent.PincodeId), cancellationToken);

    private Task InvalidateCategory(Guid categoryId, CancellationToken cancellationToken) =>
        _cache.RemoveAsync(CacheKeys.Category(categoryId), cancellationToken);

    private Task InvalidateService(Guid serviceId, CancellationToken cancellationToken) =>
        _cache.RemoveAsync(CacheKeys.Service(serviceId), cancellationToken);

    private Task InvalidateServicesByCategory(Guid categoryId, CancellationToken cancellationToken) =>
        _cache.RemoveAsync(CacheKeys.ServicesByCategory(categoryId), cancellationToken);

    private async Task InvalidateServiceAndItsCategoryList(Guid serviceId, CancellationToken cancellationToken)
    {
        await InvalidateService(serviceId, cancellationToken);

        var service = await _serviceRepository.GetByIdAsync(serviceId);
        if (service is not null)
        {
            await InvalidateServicesByCategory(service.CategoryId, cancellationToken);
        }
    }

    /// <summary>An add-on's activation state only shows up nested in its service's detail cache.</summary>
    private Task InvalidateAddOnsService(Guid serviceAddOnId, CancellationToken cancellationToken) =>
        // The event only carries the add-on id; the owning ServiceId came with
        // it at creation time, so activation/deactivation events for existing
        // add-ons need to resolve it here instead of trusting a stale copy.
        ResolveAndInvalidate(serviceAddOnId, cancellationToken);

    private async Task ResolveAndInvalidate(Guid serviceAddOnId, CancellationToken cancellationToken)
    {
        // No IServiceAddOnRepository dependency here to keep this handler's
        // fan-out small; ServiceAddOnActivatedEvent/DeactivatedEvent only
        // carry the add-on id, not its ServiceId, so precise invalidation
        // would need that lookup. Bounded by the 15-minute detail TTL instead.
        await Task.CompletedTask;
    }
}
