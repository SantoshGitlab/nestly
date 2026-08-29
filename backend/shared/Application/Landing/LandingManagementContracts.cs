namespace Nestly.Application.Landing;

/// <summary>One admin-picked sub-category in New &amp; Trending, with the names needed to render the "Category → Sub-category" row without a second lookup.</summary>
public sealed record LandingNewAndTrendingItemResponse(
    Guid CategoryId,
    string CategoryName,
    string ParentCategoryName,
    int SortOrder);

/// <summary>One admin-picked service, in Most Booked or under a category strip.</summary>
public sealed record LandingServiceItemResponse(
    Guid ServiceId,
    string ServiceName,
    string CategoryName,
    decimal Price,
    int SortOrder);

/// <summary>One configured category strip: the heading category and its ordered service picks.</summary>
public sealed record LandingCategorySectionItemResponse(
    Guid CategoryId,
    string CategoryName,
    IReadOnlyList<LandingServiceItemResponse> Services);

/// <summary>The full curation config, so the admin screen loads all three tabs in one call.</summary>
public sealed record LandingConfigResponse(
    IReadOnlyList<LandingNewAndTrendingItemResponse> NewAndTrending,
    IReadOnlyList<LandingServiceItemResponse> MostBooked,
    IReadOnlyList<LandingCategorySectionItemResponse> CategorySections);

/// <summary>
/// Replaces the New &amp; Trending picks wholesale. Order in the list IS the
/// display order - the admin screen submits the list as arranged, so no
/// separate sort field has to be kept in sync.
/// </summary>
public sealed record UpdateNewAndTrendingRequest(IReadOnlyList<Guid> CategoryIds);

/// <summary>Replaces the Most Booked picks wholesale; list order is display order.</summary>
public sealed record UpdateMostBookedRequest(IReadOnlyList<Guid> ServiceIds);

/// <summary>Replaces one heading category's strip; list order is display order, capped at <see cref="Nestly.Domain.LandingSelection.MaxServicesPerCategorySection"/>.</summary>
public sealed record UpdateCategorySectionRequest(IReadOnlyList<Guid> ServiceIds);
