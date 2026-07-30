using Nestly.Domain;

namespace Nestly.Application;

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetBySlugAsync(string slug);
    Task<bool> ExistsBySlugAsync(string slug);

    /// <summary>Active categories with an active mapping to the given city (SRS 12.9.2), ordered for display.</summary>
    Task<IReadOnlyList<Category>> ListServiceableInCityAsync(Guid cityId);
}
