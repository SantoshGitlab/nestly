using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Catalog;

/// <summary>
/// Admin CRUD over a service's priced/timed variants (Phase 3 catalog
/// redesign) - every mutation is recorded to the audit trail and explicitly
/// evicts the parent service's catalog cache entry, since <see cref="ServiceVariant"/>
/// raises no domain events of its own (see <see cref="Nestly.Domain.ServiceVariant"/>'s doc comment).
/// </summary>
public interface IServiceVariantManagementService
{
    /// <summary>Every variant of a service, for the admin management screen.</summary>
    Task<IReadOnlyList<ServiceVariantAdminResponse>> ListAsync(Guid serviceId);

    Task<Result<ServiceVariantAdminResponse>> GetByIdAsync(Guid id);
    Task<Result<ServiceVariantAdminResponse>> CreateAsync(Guid serviceId, ServiceVariantCreateRequest request);
    Task<Result<ServiceVariantAdminResponse>> UpdateAsync(Guid id, ServiceVariantUpdateRequest request);
    Task<Result> SetActiveAsync(Guid id, bool isActive);
    Task<Result> DeleteAsync(Guid id);
}
