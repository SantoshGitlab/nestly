using Nestly.Domain;

namespace Nestly.Application.Cms;

// ---------------------------------------------------------------------
// Media (task 124e)
// ---------------------------------------------------------------------

public sealed record CmsMediaResponse(Guid Id, string Url, string? AltText, DateTime CreatedAtUtc);

public sealed record CmsMediaCreateRequest(string Url, string? AltText);

public sealed record CmsMediaUpdateRequest(string Url, string? AltText);

// ---------------------------------------------------------------------
// Pages (task 124a)
// ---------------------------------------------------------------------

/// <summary>Filter criteria for the admin page list. All optional, combined with AND (mirrors <c>CouponSearchFilter</c>'s shape).</summary>
public sealed record CmsPageSearchFilter(string? Title, string? Slug, CmsContentStatus? Status, CmsPlacement? Placement, int Page, int PageSize);

public sealed record CmsPageAdminSearchRequest(string? Title, string? Slug, CmsContentStatus? Status, CmsPlacement? Placement, int Page = 1, int PageSize = 20);

public sealed record CmsPageResponse(
    Guid Id,
    string Title,
    string Slug,
    string Body,
    string? SeoTitle,
    string? SeoDescription,
    string? SeoKeywords,
    CmsPlacement Placement,
    CmsContentStatus Status,
    DateTime? PublishStartUtc,
    DateTime? PublishEndUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CmsPageSearchResult(IReadOnlyList<CmsPageResponse> Items, int TotalCount);

public sealed record CmsPageAdminSearchResponse(IReadOnlyList<CmsPageResponse> Items, int TotalCount, int Page, int PageSize);

public sealed record CmsPageCreateRequest(
    string Title,
    string Slug,
    string Body,
    string? SeoTitle,
    string? SeoDescription,
    string? SeoKeywords,
    CmsPlacement Placement,
    DateTime? PublishStartUtc,
    DateTime? PublishEndUtc);

/// <summary>Edit request for every mutable page field. New pages are always created as <see cref="CmsContentStatus.Draft"/> - see <c>CmsPageService.PublishAsync</c>/<c>UnpublishAsync</c> for the status transition.</summary>
public sealed record CmsPageUpdateRequest(
    string Title,
    string Slug,
    string Body,
    string? SeoTitle,
    string? SeoDescription,
    string? SeoKeywords,
    CmsPlacement Placement,
    DateTime? PublishStartUtc,
    DateTime? PublishEndUtc);

/// <summary>
/// Public, storefront-facing projection of a live static page - mirrors
/// <see cref="HomeBannerResponse"/>'s split from the admin CRUD shape: only
/// what a customer-facing page renders, never the draft/publish-window
/// workflow fields that decide <em>whether</em> this is returned at all.
/// </summary>
public sealed record CmsPageContentResponse(
    string Title,
    string Slug,
    string Body,
    string? SeoTitle,
    string? SeoDescription,
    DateTime UpdatedAtUtc);

// ---------------------------------------------------------------------
// Banners (task 124b)
// ---------------------------------------------------------------------

public sealed record BannerSearchFilter(CmsPlacement? Placement, CmsContentStatus? Status, Guid? CategoryId, int Page, int PageSize);

public sealed record BannerAdminSearchRequest(CmsPlacement? Placement, CmsContentStatus? Status, Guid? CategoryId, int Page = 1, int PageSize = 20);

public sealed record BannerResponse(
    Guid Id,
    string Title,
    string? Subtitle,
    Guid MediaId,
    string MediaUrl,
    string? LinkUrl,
    CmsPlacement Placement,
    Guid? CategoryId,
    string? CategoryName,
    int SortOrder,
    CmsContentStatus Status,
    DateTime? PublishStartUtc,
    DateTime? PublishEndUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record BannerSearchResult(IReadOnlyList<BannerResponse> Items, int TotalCount);

public sealed record BannerAdminSearchResponse(IReadOnlyList<BannerResponse> Items, int TotalCount, int Page, int PageSize);

/// <summary>Create request - references an existing media asset by id (task 124e's media library) rather than a bare URL, so a banner's image is always a managed, reusable asset.</summary>
public sealed record BannerCreateRequest(
    string Title,
    string? Subtitle,
    Guid MediaId,
    string? LinkUrl,
    CmsPlacement Placement,
    Guid? CategoryId,
    int SortOrder,
    DateTime? PublishStartUtc,
    DateTime? PublishEndUtc);

public sealed record BannerUpdateRequest(
    string Title,
    string? Subtitle,
    Guid MediaId,
    string? LinkUrl,
    CmsPlacement Placement,
    Guid? CategoryId,
    int SortOrder,
    DateTime? PublishStartUtc,
    DateTime? PublishEndUtc);

/// <summary>
/// Public, storefront-facing projection of a live banner (SRS 11.1.2/11.1.3):
/// only the fields the customer web home banner renders, with the media asset
/// already resolved to its URL and alt text. Deliberately omits the admin
/// workflow fields (status, publish window, sort order, category scoping) -
/// those decide <em>whether</em> a banner is returned here, they are not shown.
/// </summary>
public sealed record HomeBannerResponse(
    Guid Id,
    string Title,
    string? Subtitle,
    string ImageUrl,
    string? ImageAltText,
    string? LinkUrl);

// ---------------------------------------------------------------------
// FAQs (task 124c)
// ---------------------------------------------------------------------

public sealed record CmsFaqSearchFilter(CmsPlacement? Placement, CmsContentStatus? Status, int Page, int PageSize);

public sealed record CmsFaqAdminSearchRequest(CmsPlacement? Placement, CmsContentStatus? Status, int Page = 1, int PageSize = 20);

public sealed record CmsFaqResponse(
    Guid Id,
    string Question,
    string Answer,
    CmsPlacement Placement,
    int SortOrder,
    CmsContentStatus Status,
    DateTime? PublishStartUtc,
    DateTime? PublishEndUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CmsFaqSearchResult(IReadOnlyList<CmsFaqResponse> Items, int TotalCount);

public sealed record CmsFaqAdminSearchResponse(IReadOnlyList<CmsFaqResponse> Items, int TotalCount, int Page, int PageSize);

public sealed record CmsFaqCreateRequest(string Question, string Answer, CmsPlacement Placement, int SortOrder, DateTime? PublishStartUtc, DateTime? PublishEndUtc);

public sealed record CmsFaqUpdateRequest(string Question, string Answer, CmsPlacement Placement, int SortOrder, DateTime? PublishStartUtc, DateTime? PublishEndUtc);
