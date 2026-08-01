namespace Nestly.Application.Catalog;

/// <summary>Category card for a listing page (SRS 11.1/11.5), scoped to a serviceable city.</summary>
public record CategorySummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? IconUrl,
    string? BannerUrl,
    bool IsFeatured);

public record ServiceAddOnSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price);

/// <summary>A service as it appears nested under a category detail (SRS 11.5).</summary>
public record ServiceSummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    IReadOnlyList<ServiceAddOnSummaryResponse> AddOns);

/// <summary>Category detail: the category plus its active services and their add-ons (task 41, SRS 11.5).</summary>
public record CategoryDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string? IconUrl,
    string? BannerUrl,
    IReadOnlyList<ServiceSummaryResponse> Services);

/// <summary>Service card for a "services within category" listing (task 42a, SRS 11.5.3).</summary>
public record ServiceListItemResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    decimal Price);

/// <summary>One FAQ entry on a service detail page (task 52d, SRS 11.6.1).</summary>
public record ServiceFaqResponse(Guid Id, string Question, string Answer);

/// <summary>Full service detail page content (task 42b, SRS 11.6.1).</summary>
public record ServiceDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    string Inclusions,
    string Exclusions,
    string? CancellationPolicy,
    string? ReschedulePolicy,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    IReadOnlyList<ServiceAddOnSummaryResponse> AddOns,
    IReadOnlyList<ServiceFaqResponse> Faqs);

/// <summary>One recent review shown in a service's rating summary (task 52f, SRS 11.6.1).</summary>
public record ServiceReviewItemResponse(Guid Id, int Rating, string? ReviewText, DateTime CreatedAtUtc);

/// <summary>
/// Rating summary for a service detail page (task 52f, SRS 11.6.1 "Reviews
/// and rating summary") - only visible (non-hidden) reviews, same as
/// <see cref="Nestly.Application.Reviews.IReviewRepository.ListByServiceAsync"/>.
/// </summary>
public record ServiceReviewSummaryResponse(
    double AverageRating,
    int TotalCount,
    IReadOnlyDictionary<int, int> RatingBreakdown,
    IReadOnlyList<ServiceReviewItemResponse> RecentReviews);

/// <summary>Combined category/service search results (task 42c, SRS 11.5-11.6, 24.3).</summary>
public record CatalogSearchResponse(
    IReadOnlyList<CategorySummaryResponse> Categories,
    IReadOnlyList<ServiceListItemResponse> Services);
