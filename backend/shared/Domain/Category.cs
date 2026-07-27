using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

public class Category : Entity<Guid>
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

    protected Category() { }

    public Category(Guid id, string name, string slug, string description) : base(id)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Slug = slug ?? throw new ArgumentNullException(nameof(slug));
        Description = description ?? string.Empty;
        IsActive = true;
        IsFeatured = false;
        SortOrder = 0;
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
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
    public void Feature() => IsFeatured = true;
    public void Unfeature() => IsFeatured = false;
}
