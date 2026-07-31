using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Join between <see cref="AdminRole"/> and <see cref="AdminPermission"/>: the
/// permission matrix grant (SRS 12.2.3). A role may hold many permissions and
/// a permission may be granted to many roles.
/// </summary>
public class RolePermissionMapping : Entity<Guid>
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    protected RolePermissionMapping() { }

    public RolePermissionMapping(Guid id, Guid roleId, Guid permissionId) : base(id)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        CreatedAt = DateTime.UtcNow;
    }
}
