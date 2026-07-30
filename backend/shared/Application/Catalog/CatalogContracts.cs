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
