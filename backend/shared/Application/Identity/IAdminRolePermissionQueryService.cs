namespace Nestly.Application.Identity;

/// <summary>Resolved permission grant for an admin login (task 96c) — the role's name and every permission code it holds.</summary>
/// <param name="RoleName">Null when the account has no <see cref="Nestly.Domain.AdminUser.RoleId"/> assigned yet.</param>
/// <param name="PermissionCodes">Empty (never null) when there is no role — an unassigned account has no permissions.</param>
public sealed record AdminRolePermissions(string? RoleName, IReadOnlyList<string> PermissionCodes);

/// <summary>
/// Looks up the permission grant behind an admin user's assigned role (SRS
/// 12.2.3), read at login time so <see cref="IAdminTokenService"/> can embed
/// it as JWT claims (task 96c). Kept separate from <see cref="IAdminTokenService"/>
/// itself so token issuance stays a pure, DB-free operation — the same
/// separation <see cref="ITokenService"/> keeps from the customer login path.
/// </summary>
public interface IAdminRolePermissionQueryService
{
    Task<AdminRolePermissions> GetPermissionsAsync(Guid? roleId);
}
