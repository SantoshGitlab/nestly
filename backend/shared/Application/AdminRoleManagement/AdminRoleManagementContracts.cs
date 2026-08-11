using Nestly.Domain;

namespace Nestly.Application.AdminRoleManagement;

/// <summary>
/// One grantable permission code from the catalog (SRS 12.2.3), for the
/// admin permission-matrix editor UI. Mirrors <see cref="AdminPermissionDefinition"/>
/// - a separate DTO rather than exposing the domain record directly, same
/// reasoning as every other admin response type in this codebase.
/// </summary>
public sealed record AdminPermissionCatalogEntryResponse(string Code, string Module, AdminPermissionAction Action, string Description);

/// <summary>Full detail of one <see cref="AdminRole"/>: its identity plus the exact set of permission codes it currently grants (SRS 12.2.2, 12.2.3).</summary>
public sealed record AdminRoleDetailResponse(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<string> PermissionCodes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Creates a new role (SRS 12.2.2 "roles are configurable, not just the nine
/// seeded ones"). <see cref="PermissionCodes"/> is the initial permission-matrix
/// row - empty is valid (an unusable role until permissions are granted).
/// </summary>
public sealed record CreateAdminRoleRequest(string Name, string Description, IReadOnlyList<string> PermissionCodes);

/// <summary>Renames a role / edits its description (SRS 12.2.2). Permissions are edited separately via <see cref="SetAdminRolePermissionsRequest"/> - the same split <c>AdminUsersController</c> uses between profile edits and role assignment.</summary>
public sealed record UpdateAdminRoleRequest(string Name, string Description);

/// <summary>
/// Replaces a role's entire permission-matrix row (SRS 12.2.3) with exactly
/// this set of codes - a full-replace rather than an add/remove delta, so the
/// permission-matrix editor UI can always show (and submit) the complete grid
/// state for one role.
/// </summary>
public sealed record SetAdminRolePermissionsRequest(IReadOnlyList<string> PermissionCodes);
