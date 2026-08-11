using Nestly.Domain;

namespace Nestly.Application;

/// <summary>
/// Access to <see cref="AdminRole"/>: read for the admin-user role-assignment
/// picker (SRS 12.2.1, task 97b), and full CRUD for role management itself
/// (SRS 12.2.2, task 313) via the inherited <see cref="IRepository{T}"/>
/// members plus <see cref="GetByNameAsync"/> for uniqueness checks.
/// </summary>
public interface IAdminRoleRepository : IRepository<AdminRole>
{
    /// <summary>Every seeded/created role, ordered by name, for the assignment picker.</summary>
    Task<IReadOnlyList<AdminRole>> ListAllAsync();

    /// <summary>Looks up a role by its exact name (case-insensitive) - <see cref="AdminRole.Name"/> carries a unique index, so this backs the create/rename duplicate-name check (task 313).</summary>
    Task<AdminRole?> GetByNameAsync(string name);
}
