using Nestly.Domain;

namespace Nestly.Application;

public interface IAdminUserRepository : IRepository<AdminUser>
{
    Task<AdminUser?> GetByEmailAsync(string email);
}
