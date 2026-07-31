using Nestly.Domain;

namespace Nestly.Application.AdminUserManagement;

/// <summary>
/// Search/filter criteria for the admin-user list (SRS 12.2.1, task 97a). All
/// filters are optional and combine with AND; string filters are
/// case-insensitive substring matches - mirrors <c>CustomerSearchFilter</c>'s
/// shape (backend/shared/Application/Customers/CustomerManagementContracts.cs).
/// </summary>
public sealed record AdminUserSearchFilter(
    string? Email,
    string? Name,
    AdminUserStatus? Status,
    Guid? RoleId,
    int Page,
    int PageSize);

/// <summary>One admin-user row from a search, with its role name resolved (null when unassigned).</summary>
public sealed record AdminUserSearchRow(AdminUser AdminUser, string? RoleName);

/// <summary>A page of <see cref="AdminUserSearchRow"/> plus the total match count, for pagination.</summary>
public sealed record AdminUserSearchResult(IReadOnlyList<AdminUserSearchRow> Rows, int TotalCount);

/// <summary>Query-string shape of an admin-user search request (task 97a).</summary>
public sealed record AdminUserSearchRequest(
    string? Email,
    string? Name,
    AdminUserStatus? Status,
    Guid? RoleId,
    int Page = 1,
    int PageSize = 20);

public sealed record AdminUserSummaryResponse(
    Guid Id,
    string Email,
    string FullName,
    AdminUserStatus Status,
    Guid? RoleId,
    string? RoleName,
    bool IsLockedOut,
    DateTime CreatedAtUtc);

public sealed record AdminUserSearchResponse(
    IReadOnlyList<AdminUserSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Full detail view of one admin account (task 97a "get").</summary>
public sealed record AdminUserDetailResponse(
    Guid Id,
    string Email,
    string FullName,
    AdminUserStatus Status,
    Guid? RoleId,
    string? RoleName,
    bool IsLockedOut,
    DateTime? LockedUntilUtc,
    int FailedLoginAttempts,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Provisions a new admin account (task 97a "create"). <see cref="Password"/> must satisfy <c>AdminPasswordPolicy</c>.</summary>
public sealed record CreateAdminUserRequest(string Email, string FullName, string Password, Guid? RoleId);

/// <summary>Edits an existing admin account's profile fields (task 97a "update") - email and name, not status or role (those are their own dedicated actions, 97b/97c).</summary>
public sealed record UpdateAdminUserRequest(string Email, string FullName);

/// <summary>Assigns (or clears, with null) an admin account's role (SRS 12.2.1 "Assign role(s)", task 97b).</summary>
public sealed record AssignAdminRoleRequest(Guid? RoleId);

/// <summary>
/// Result of an admin-initiated password reset (SRS 12.2.1 "Reset password",
/// task 97d): a freshly generated temporary password, returned once in the
/// response body for the initiating Super Admin to relay to the account
/// owner out of band - the account's <c>PasswordHash</c> is updated in the
/// same operation via the same <c>PasswordHasher&lt;AdminUser&gt;</c> the
/// self-service admin login path already uses (<c>AdminLoginService</c>), so
/// no new hashing scheme is introduced.
/// </summary>
public sealed record ResetAdminPasswordResponse(string TemporaryPassword);

/// <summary>Read-only role summary for the role-assignment picker (task 97b) - role CRUD itself is out of scope here (SRS 12.2.2, a separate task).</summary>
public sealed record AdminRoleSummaryResponse(Guid Id, string Name, string Description);
