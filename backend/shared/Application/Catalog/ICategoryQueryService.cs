using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Catalog;

/// <summary>Public read-only category catalog queries (task 41, SRS 11.1/11.5).</summary>
public interface ICategoryQueryService
{
    /// <summary>
    /// Active categories serviceable in the given city, ordered for display.
    /// When <paramref name="pincodeId"/> is given, narrowed to categories
    /// with at least one active service actually reaching that pincode
    /// (SRS 11.1.3's "filtered by selected city/serviceability") - the
    /// customer picked a specific area, not just the city.
    /// </summary>
    Task<Result<IReadOnlyList<CategorySummaryResponse>>> ListServiceableInCityAsync(Guid cityId, Guid? pincodeId = null);

    /// <summary>Category detail with its active services and their add-ons.</summary>
    Task<Result<CategoryDetailResponse>> GetDetailBySlugAsync(string slug);
}
