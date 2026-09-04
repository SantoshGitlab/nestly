using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

public sealed record CategoryCreatedEvent(Guid CategoryId) : DomainEvent;

public sealed record CategoryActivatedEvent(Guid CategoryId) : DomainEvent;

public sealed record CategoryDeactivatedEvent(Guid CategoryId) : DomainEvent;

public sealed record CategoryParentChangedEvent(Guid CategoryId, Guid? OldParentCategoryId, Guid? NewParentCategoryId) : DomainEvent;

/// <summary>
/// A general-field edit (name, description, icon/banner, SEO, sort order,
/// category group) - mirrors <c>ServiceUpdatedEvent</c>. Raised unconditionally
/// by <see cref="Category.MarkUpdated"/> so <c>CatalogCacheInvalidationHandler</c>
/// can bust the cached detail response even when the change is a plain field
/// (not the structural parent/active/featured changes the other events cover).
/// </summary>
public sealed record CategoryUpdatedEvent(Guid CategoryId, Guid? ParentCategoryId) : DomainEvent;
