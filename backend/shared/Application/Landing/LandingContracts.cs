namespace Nestly.Application.Landing;

/// <summary>
/// A sub-category card in "New &amp; Trending" - image and name only. No price
/// is carried here by design: this section links into a category, not a
/// bookable service, so there is no single price to show.
/// </summary>
public sealed record LandingSubCategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? ImageUrl,
    /// <summary>The top-level category this sits under, for the "Category → Sub-category" label.</summary>
    string ParentCategoryName);

/// <summary>A bookable-service card ("Most Booked", category strips) - the same image/title/price triple the existing service card renders.</summary>
public sealed record LandingServiceResponse(
    Guid Id,
    string Name,
    string Slug,
    string? ImageUrl,
    decimal Price);

/// <summary>One category-wise strip: the heading category plus its admin-picked services (at most <see cref="Nestly.Domain.LandingSelection.MaxServicesPerCategorySection"/>).</summary>
public sealed record LandingCategorySectionResponse(
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    IReadOnlyList<LandingServiceResponse> Services);

/// <summary>
/// The whole curated home page in one response, so the landing page makes a
/// single call rather than one per section. Sections the admin has not
/// configured come back as empty lists, never null - the page then simply
/// renders nothing for them instead of branching on null.
/// </summary>
public sealed record HomeLandingResponse(
    IReadOnlyList<LandingSubCategoryResponse> NewAndTrending,
    IReadOnlyList<LandingServiceResponse> MostBooked,
    IReadOnlyList<LandingCategorySectionResponse> CategorySections);
