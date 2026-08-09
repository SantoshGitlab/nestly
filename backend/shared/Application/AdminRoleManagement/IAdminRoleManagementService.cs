using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.AdminRoleManagement;

/// <summary>
/// Role CRUD and permission-matrix editing (SRS 12.2.2, 12.2.3, task 313).
/// Before this, <see cref="Nestly.Domain.AdminPermissionCatalog"/>'s nine
/// seeded roles and their grants were compile-time constants - changing who
/// could do what required a code change and redeploy. This service makes
/// <see cref="Nestly.Domain.AdminRole"/> and <see cref="Nestly.Domain.RolePermissionMapping"/>
/// genuinely writable at runtime, gated behind the same "settings.write"
/// policy <c>AdminUsersController</c> already uses for admin-user
/// administration.
///
/// Every permission-granting write here enforces a self-escalation guard: an
/// admin can never cause a role to hold a permission code the admin does not
/// already hold themselves (see <c>AdminRoleManagementService</c>'s
/// doc comment for the exact rule) - the single most important correctness
/// property of this feature.
/// </summary>
public interface IAdminRoleManagementService
{
    /// <summary>Every grantable permission code (module x action), for the permission-matrix editor's grid.</summary>
    Task<Result<IReadOnlyList<AdminPermissionCatalogEntryResponse>>> GetPermissionCatalogAsync();

    /// <summary>Every role with its currently granted permission codes.</summary>
    Task<Result<IReadOnlyList<AdminRoleDetailResponse>>> ListAsync();

    Task<Result<AdminRoleDetailResponse>> GetByIdAsync(Guid roleId);

    /// <summary>Creates a new role with an initial permission-matrix row. <paramref name="actingAdminUserId"/> is subject to the self-escalation guard.</summary>
    Task<Result<AdminRoleDetailResponse>> CreateAsync(CreateAdminRoleRequest request, Guid actingAdminUserId);

    /// <summary>Renames a role / edits its description - does not touch its permission grants.</summary>
    Task<Result<AdminRoleDetailResponse>> UpdateAsync(Guid roleId, UpdateAdminRoleRequest request, Guid actingAdminUserId);

    /// <summary>Replaces a role's full permission-matrix row, subject to the self-escalation guard.</summary>
    Task<Result<AdminRoleDetailResponse>> SetPermissionsAsync(Guid roleId, SetAdminRolePermissionsRequest request, Guid actingAdminUserId);
}
