using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Catalog;

/// <summary>Public read-only service catalog queries (tasks 42a/42b, SRS 11.5-11.6).</summary>
public interface IServiceQueryService
{
    /// <summary>Active services under a category, ordered for display (task 42a).</summary>
    Task<Result<IReadOnlyList<ServiceListItemResponse>>> ListByCategoryAsync(Guid categoryId);

    /// <summary>Full service detail: inclusions/exclusions/add-ons/policies/FAQs (task 42b, 52d).</summary>
    Task<Result<ServiceDetailResponse>> GetDetailBySlugAsync(string slug);

    /// <summary>Rating summary and recent reviews for a service detail page (task 52f, SRS 11.6.1).</summary>
    Task<Result<ServiceReviewSummaryResponse>> GetReviewSummaryBySlugAsync(string slug);
}
