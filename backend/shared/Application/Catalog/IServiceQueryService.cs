using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Catalog;

/// <summary>Public read-only service catalog queries (tasks 42a/42b, SRS 11.5-11.6).</summary>
public interface IServiceQueryService
{
    /// <summary>Active services under a category, ordered for display (task 42a).</summary>
    Task<Result<IReadOnlyList<ServiceListItemResponse>>> ListByCategoryAsync(Guid categoryId);

    /// <summary>Full service detail: inclusions/exclusions/add-ons/policies (task 42b).</summary>
    Task<Result<ServiceDetailResponse>> GetDetailBySlugAsync(string slug);
}
