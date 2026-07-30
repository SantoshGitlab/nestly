using Nestly.Domain;

namespace Nestly.Application;

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetBySlugAsync(string slug);
    Task<bool> ExistsBySlugAsync(string slug);
}
