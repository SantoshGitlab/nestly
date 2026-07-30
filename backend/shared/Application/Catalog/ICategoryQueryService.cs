using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Catalog;

/// <summary>Public read-only category catalog queries (task 41, SRS 11.1/11.5).</summary>
public interface ICategoryQueryService
{
    /// <summary>Active categories serviceable in the given city, ordered for display.</summary>
    Task<Result<IReadOnlyList<CategorySummaryResponse>>> ListServiceableInCityAsync(Guid cityId);

    /// <summary>Category detail with its active services and their add-ons.</summary>
    Task<Result<CategoryDetailResponse>> GetDetailBySlugAsync(string slug);
}
