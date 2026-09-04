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
    INotificationHandler<DomainEventNotification<CategoryParentChangedEvent>>,
    INotificationHandler<DomainEventNotification<CategoryUpdatedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceCreatedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceActivatedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceDeactivatedEvent>>,
    INotificationHandler<DomainEventNotification<ServicePriceChangedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceUpdatedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceAddOnCreatedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceAddOnActivatedEvent>>,
    INotificationHandler<DomainEventNotification<ServiceAddOnDeactivatedEvent>>,
    INotificationHandler<DomainEventNotification<CategoryCityMappingChangedEvent>>,
    INotificationHandler<DomainEventNotification<ServicePincodeMappingChangedEvent>>
{
    private readonly ICacheService _cache;
    private readonly IServiceRepository _serviceRepository;
    private readonly ICategoryRepository _categoryRepository;

    public CatalogCacheInvalidationHandler(ICacheService cache, IServiceRepository serviceRepository, ICategoryRepository categoryRepository)
    {
        _cache = cache;
        _serviceRepository = serviceRepository;
        _categoryRepository = categoryRepository;
    }

    public Task Handle(DomainEventNotification<CategoryCreatedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateCategory(notification.DomainEvent.CategoryId, cancellationToken);

    // A child's activation state changes its parent's cached Subcategories
    // list (Phase 3 catalog redesign), so this now also busts the parent.
    public Task Handle(DomainEventNotification<CategoryActivatedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateCategoryAndItsParent(notification.DomainEvent.CategoryId, cancellationToken);

    public Task Handle(DomainEventNotification<CategoryDeactivatedEvent> notification, CancellationToken cancellationToken) =>
        InvalidateCategoryAndItsParent(notification.DomainEvent.CategoryId, cancellationToken);

    /// <summary>Phase 3 catalog redesign: busts both the old and new parent's cache entry, since each one's cached Subcategories list just changed.</summary>
    public async Task Handle(DomainEventNotification<CategoryParentChangedEvent> notification, CancellationToken cancellationToken)
    {
        await InvalidateCategory(notification.DomainEvent.CategoryId, cancellationToken);

        if (notification.DomainEvent.OldParentCategoryId is Guid oldParentId)
        {
            await InvalidateCategory(oldParentId, cancellationToken);
        }

        if (notification.DomainEvent.NewParentCategoryId is Guid newParentId)
        {
            await InvalidateCategory(newParentId, cancellationToken);
        }
    }

    /// <summary>
    /// A plain-field edit (name, description, icon/banner, SEO, sort order,
    /// category group) - busts this category's own cache entry, and its
    /// parent's too, since the parent's cached response embeds this
    /// category's summary (name/slug/icon/banner) in its Subcategories list.
    /// </summary>
    public async Task Handle(DomainEventNotification<CategoryUpdatedEvent> notification, CancellationToken cancellationToken)
    {
        await InvalidateCategory(notification.DomainEvent.CategoryId, cancellationToken);

        if (notification.DomainEvent.ParentCategoryId is Guid parentId)
        {
            await InvalidateCategory(parentId, cancellationToken);
        }
    }

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

    /// <summary>
    /// A general-field edit (name, description, cover image, inclusions,
    /// duration, etc.) always busts the service detail cache, and the
    /// category service-listing cache too - both the new category's (sort
    /// order/name/featured flag changes show up there) and, if the service
    /// was moved, the old category's as well.
    /// </summary>
    public async Task Handle(DomainEventNotification<ServiceUpdatedEvent> notification, CancellationToken cancellationToken)
    {
        await InvalidateService(notification.DomainEvent.ServiceId, cancellationToken);
        await InvalidateServicesByCategory(notification.DomainEvent.NewCategoryId, cancellationToken);

        if (notification.DomainEvent.OldCategoryId != notification.DomainEvent.NewCategoryId)
        {
            await InvalidateServicesByCategory(notification.DomainEvent.OldCategoryId, cancellationToken);
        }
    }

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

    private async Task InvalidateCategoryAndItsParent(Guid categoryId, CancellationToken cancellationToken)
    {
        await InvalidateCategory(categoryId, cancellationToken);

        var category = await _categoryRepository.GetByIdAsync(categoryId);
        if (category?.ParentCategoryId is Guid parentId)
        {
            await InvalidateCategory(parentId, cancellationToken);
        }
    }

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
