using Nestly.Domain;

namespace Nestly.Application;

/// <summary>
/// Read access to <see cref="AdminRole"/> for the admin-user role-assignment
/// picker (SRS 12.2.1, task 97b). Role CRUD itself (SRS 12.2.2) is a separate,
/// not-yet-built task - this interface stays read-only until that lands.
/// </summary>
public interface IAdminRoleRepository : IRepository<AdminRole>
{
    /// <summary>Every seeded/created role, ordered by name, for the assignment picker.</summary>
    Task<IReadOnlyList<AdminRole>> ListAllAsync();
}
