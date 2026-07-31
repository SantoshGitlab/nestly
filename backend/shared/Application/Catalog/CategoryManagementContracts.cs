namespace Nestly.Application.Catalog;

/// <summary>Admin view of a category (SRS 12.5.2), including SEO/media fields not shown to consumers.</summary>
public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string? IconUrl,
    string? BannerUrl,
    bool IsActive,
    bool IsFeatured,
    int SortOrder,
    string? SeoTitle,
    string? SeoMetaDescription);

/// <summary>Admin create request for a category (SRS 12.5.1/12.5.2).</summary>
public sealed record CategoryCreateRequest(
    string Name,
    string Slug,
    string Description,
    string? IconUrl,
    string? BannerUrl,
    int SortOrder,
    string? SeoTitle,
    string? SeoMetaDescription);

/// <summary>
/// Admin update request for a category. Covers every editable field (SRS
/// 12.5.2) except <see cref="Category.IsActive"/>/<see cref="Category.IsFeatured"/>,
/// which are toggled through their own dedicated endpoints so each change is
/// audited as its own distinct action.
/// </summary>
public sealed record CategoryUpdateRequest(
    string Name,
    string Slug,
    string Description,
    string? IconUrl,
    string? BannerUrl,
    int SortOrder,
    string? SeoTitle,
    string? SeoMetaDescription);
