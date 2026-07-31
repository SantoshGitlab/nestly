using Microsoft.EntityFrameworkCore;
using Nestly.Application.Identity;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Reads the permission grant behind an admin user's role directly from
/// <c>role_permission_mapping</c>/<c>admin_permission</c> (task 96c). A
/// read-only query service, same shape as <c>ICategoryQueryService</c> and
/// friends — no caching yet: permission checks happen once per login, not
/// per request, so the extra round trip is not on any hot path.
/// </summary>
public sealed class AdminRolePermissionQueryService : IAdminRolePermissionQueryService
{
    private readonly NestlyDbContext _context;

    public AdminRolePermissionQueryService(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<AdminRolePermissions> GetPermissionsAsync(Guid? roleId)
    {
        if (roleId is null)
        {
            return new AdminRolePermissions(RoleName: null, PermissionCodes: Array.Empty<string>());
        }

        AdminRole? role = await _context.Set<AdminRole>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId.Value);
        if (role is null)
        {
            // The FK is SetNull-on-delete, so a stale RoleId here would mean
            // a concurrent delete raced this read - treat it the same as
            // "no role" rather than throwing.
            return new AdminRolePermissions(RoleName: null, PermissionCodes: Array.Empty<string>());
        }

        List<string> permissionCodes = await (
            from mapping in _context.Set<RolePermissionMapping>().AsNoTracking()
            join permission in _context.Set<AdminPermission>().AsNoTracking()
                on mapping.PermissionId equals permission.Id
            where mapping.RoleId == roleId.Value
            select permission.Code).ToListAsync();

        return new AdminRolePermissions(role.Name, permissionCodes);
    }
}
