using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Catalog;

/// <summary>
/// Admin CRUD over categories (SRS 12.5): creation, editing, display
/// ordering, media (icon/banner) and SEO fields, and activation - every
/// mutation is recorded to the audit trail (<see cref="Nestly.Application.Abstractions.Auditing.IAuditLogWriter"/>).
/// </summary>
public interface ICategoryManagementService
{
    Task<IReadOnlyList<CategoryResponse>> ListAsync();
    Task<Result<CategoryResponse>> GetByIdAsync(Guid id);
    Task<Result<CategoryResponse>> CreateAsync(CategoryCreateRequest request);
    Task<Result<CategoryResponse>> UpdateAsync(Guid id, CategoryUpdateRequest request);
    Task<Result> SetActiveAsync(Guid id, bool isActive);
    Task<Result> SetFeaturedAsync(Guid id, bool isFeatured);
}
