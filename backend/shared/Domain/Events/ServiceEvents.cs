using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

public sealed record ServiceCreatedEvent(Guid ServiceId, Guid CategoryId) : DomainEvent;

public sealed record ServiceActivatedEvent(Guid ServiceId) : DomainEvent;

public sealed record ServiceDeactivatedEvent(Guid ServiceId) : DomainEvent;

public sealed record ServicePriceChangedEvent(Guid ServiceId, decimal OldPrice, decimal NewPrice) : DomainEvent;

/// <summary>
/// Raised once per admin edit of a service's general fields (name,
/// description, cover image, inclusions, duration, etc.), after
/// ServiceManagementService.UpdateAsync has applied every field setter.
/// Distinct from ServicePriceChangedEvent/ServiceActivatedEvent/
/// ServiceDeactivatedEvent so cache invalidation isn't limited to just
/// those three cases - a plain field edit was previously invisible to
/// CatalogCacheInvalidationHandler, leaving the service detail cache stale
/// for up to its TTL.
/// </summary>
public sealed record ServiceUpdatedEvent(Guid ServiceId, Guid OldCategoryId, Guid NewCategoryId) : DomainEvent;
