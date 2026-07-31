using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.AdminUserManagement;

/// <summary>
/// Admin user management (SRS 12.2.1, tasks 97a-97d): CRUD over
/// <see cref="Nestly.Domain.AdminUser"/>, role assignment, activate/deactivate,
/// and admin-initiated password reset - one Super Admin managing another
/// back-office operator's account. Mirrors <c>ICustomerManagementService</c>'s
/// shape (one service over one aggregate's admin actions) but is its own
/// interface: the two manage different aggregates with different lifecycle
/// rules (an admin account is provisioned rather than self-registered, and
/// carries role/permission state a customer account has no equivalent of).
/// </summary>
public interface IAdminUserManagementService
{
    Task<Result<AdminUserSearchResponse>> SearchAsync(AdminUserSearchRequest request);

    Task<Result<AdminUserDetailResponse>> GetByIdAsync(Guid adminUserId);

    /// <summary>Creates a new admin account (task 97a). <paramref name="actingAdminUserId"/> is the Super Admin performing the action, for audit.</summary>
    Task<Result<AdminUserDetailResponse>> CreateAsync(CreateAdminUserRequest request, Guid actingAdminUserId);

    /// <summary>Edits an existing admin account's profile (task 97a).</summary>
    Task<Result<AdminUserDetailResponse>> UpdateAsync(Guid adminUserId, UpdateAdminUserRequest request, Guid actingAdminUserId);

    /// <summary>Assigns or clears an admin account's role (task 97b).</summary>
    Task<Result<AdminUserDetailResponse>> AssignRoleAsync(Guid adminUserId, AssignAdminRoleRequest request, Guid actingAdminUserId);

    /// <summary>Activates a deactivated admin account (task 97c) - distinct from clearing a lockout (task 95d's <c>IAdminLoginService.UnlockAsync</c>).</summary>
    Task<Result<AdminUserDetailResponse>> ActivateAsync(Guid adminUserId, Guid actingAdminUserId);

    /// <summary>Deactivates an admin account so it can no longer log in (task 97c), without touching its lockout state.</summary>
    Task<Result<AdminUserDetailResponse>> DeactivateAsync(Guid adminUserId, Guid actingAdminUserId);

    /// <summary>Generates a new temporary password for the account and returns it once (task 97d, SRS 12.2.1 "Reset password").</summary>
    Task<Result<ResetAdminPasswordResponse>> ResetPasswordAsync(Guid adminUserId, Guid actingAdminUserId);

    /// <summary>Every seeded/created role, for the role-assignment picker (task 97b).</summary>
    Task<Result<IReadOnlyList<AdminRoleSummaryResponse>>> ListRolesAsync();
}
