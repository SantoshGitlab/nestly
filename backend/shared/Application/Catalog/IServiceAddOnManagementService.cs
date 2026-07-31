using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Catalog;

/// <summary>
/// Admin CRUD over add-ons and their mapping to services (SRS 12.7) - every
/// mutation is recorded to the audit trail (<see cref="Nestly.Application.Abstractions.Auditing.IAuditLogWriter"/>).
/// </summary>
public interface IServiceAddOnManagementService
{
    /// <summary>All add-ons, optionally filtered to one service, for the admin list screen.</summary>
    Task<IReadOnlyList<ServiceAddOnAdminResponse>> ListAsync(Guid? serviceId);

    Task<Result<ServiceAddOnAdminResponse>> GetByIdAsync(Guid id);
    Task<Result<ServiceAddOnAdminResponse>> CreateAsync(ServiceAddOnCreateRequest request);
    Task<Result<ServiceAddOnAdminResponse>> UpdateAsync(Guid id, ServiceAddOnUpdateRequest request);
    Task<Result> SetActiveAsync(Guid id, bool isActive);
}
