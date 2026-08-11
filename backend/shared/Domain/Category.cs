using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain.Events;

namespace Nestly.Domain;

public class Category : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? IconUrl { get; private set; }
    public string? BannerUrl { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsFeatured { get; private set; }
    public int SortOrder { get; private set; }
    public string? SeoTitle { get; private set; }
    public string? SeoMetaDescription { get; private set; }

    /// <summary>Null for a top-level category. Set via <see cref="SetParent"/>, never in the constructor, so an existing category's tree position is always an explicit, event-raising change.</summary>
    public Guid? ParentCategoryId { get; private set; }

    protected Category() { }

    public Category(Guid id, string name, string slug, string description) : base(id)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Slug = slug ?? throw new ArgumentNullException(nameof(slug));
        Description = description ?? string.Empty;
        IsActive = true;
        IsFeatured = false;
        SortOrder = 0;
        RaiseDomainEvent(new CategoryCreatedEvent(Id));
    }

    public void SetName(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void SetSlug(string slug) => Slug = slug ?? throw new ArgumentNullException(nameof(slug));
    public void SetDescription(string d) => Description = d ?? string.Empty;
    public void SetIconUrl(string? url) => IconUrl = url;
    public void SetBannerUrl(string? url) => BannerUrl = url;
    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
    public void SetSeo(string? title, string? metaDescription)
    {
        SeoTitle = title;
        SeoMetaDescription = metaDescription;
    }
    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        RaiseDomainEvent(new CategoryActivatedEvent(Id));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        RaiseDomainEvent(new CategoryDeactivatedEvent(Id));
    }
    public void Feature() => IsFeatured = true;
    public void Unfeature() => IsFeatured = false;

    /// <summary>
    /// Sets or clears this category's parent (Phase 3 catalog redesign).
    /// Only the immediate self-parent case is checked here; a deeper
    /// transitive-ancestor cycle check needs repository access and lives in
    /// CategoryManagementService.
    /// </summary>
    public void SetParent(Guid? parentCategoryId)
    {
        if (parentCategoryId == Id)
        {
            throw new ArgumentException("A category cannot be its own parent.", nameof(parentCategoryId));
        }

        if (parentCategoryId == ParentCategoryId)
        {
            return;
        }

        var oldParentCategoryId = ParentCategoryId;
        ParentCategoryId = parentCategoryId;
        RaiseDomainEvent(new CategoryParentChangedEvent(Id, oldParentCategoryId, parentCategoryId));
    }
}
