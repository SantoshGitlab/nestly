using Nestly.Application.AdminUserManagement;
using Nestly.Domain;

namespace Nestly.Application;

public interface IAdminUserRepository : IRepository<AdminUser>
{
    Task<AdminUser?> GetByEmailAsync(string email);

    /// <summary>Every active admin, for pickers like the support ticket "assign to" dropdown (task 120a) - inactive accounts are excluded since a ticket should never be assignable to someone who can no longer log in.</summary>
    Task<IReadOnlyList<AdminUser>> ListActiveAsync();

    /// <summary>Search/filter with pagination (SRS 12.2.1, task 97a) - see <see cref="AdminUserSearchFilter"/> for the supported criteria.</summary>
    Task<AdminUserSearchResult> SearchAsync(AdminUserSearchFilter filter);
}
