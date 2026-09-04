using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Catalog;

/// <summary>Admin CRUD over a category's optional subcategory-group section headers.</summary>
public interface ICategoryGroupManagementService
{
    Task<IReadOnlyList<CategoryGroupAdminResponse>> ListAsync(Guid? categoryId);
    Task<Result<CategoryGroupAdminResponse>> GetByIdAsync(Guid id);
    Task<Result<CategoryGroupAdminResponse>> CreateAsync(CategoryGroupCreateRequest request);
    Task<Result<CategoryGroupAdminResponse>> UpdateAsync(Guid id, CategoryGroupUpdateRequest request);
    Task<Result> SetActiveAsync(Guid id, bool isActive);
    Task<Result> DeleteAsync(Guid id);
}
