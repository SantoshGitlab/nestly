using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Catalog;

/// <summary>Admin CRUD over a category's optional service-group section headers.</summary>
public interface IServiceGroupManagementService
{
    Task<IReadOnlyList<ServiceGroupAdminResponse>> ListAsync(Guid? categoryId);
    Task<Result<ServiceGroupAdminResponse>> GetByIdAsync(Guid id);
    Task<Result<ServiceGroupAdminResponse>> CreateAsync(ServiceGroupCreateRequest request);
    Task<Result<ServiceGroupAdminResponse>> UpdateAsync(Guid id, ServiceGroupUpdateRequest request);
    Task<Result> SetActiveAsync(Guid id, bool isActive);
    Task<Result> DeleteAsync(Guid id);
}
