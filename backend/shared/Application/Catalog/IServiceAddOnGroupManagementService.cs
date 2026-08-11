using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Catalog;

/// <summary>
/// Admin CRUD over add-on groups and their selection rules (Phase 3 catalog
/// redesign) - every mutation is recorded to the audit trail and explicitly
/// evicts the parent service's catalog cache entry, since <see cref="Nestly.Domain.ServiceAddOnGroup"/>
/// raises no domain events of its own.
/// </summary>
public interface IServiceAddOnGroupManagementService
{
    /// <summary>All add-on groups, optionally filtered to one service, for the admin list screen.</summary>
    Task<IReadOnlyList<ServiceAddOnGroupAdminResponse>> ListAsync(Guid? serviceId);

    Task<Result<ServiceAddOnGroupAdminResponse>> GetByIdAsync(Guid id);
    Task<Result<ServiceAddOnGroupAdminResponse>> CreateAsync(ServiceAddOnGroupCreateRequest request);
    Task<Result<ServiceAddOnGroupAdminResponse>> UpdateAsync(Guid id, ServiceAddOnGroupUpdateRequest request);

    /// <summary>Fails with a conflict when the group still has add-ons pointing at it - those must be ungrouped or reassigned first.</summary>
    Task<Result> DeleteAsync(Guid id);
}
