using Nestly.Domain;

namespace Nestly.Application;

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetBySlugAsync(string slug);
    Task<bool> ExistsBySlugAsync(string slug);

    /// <summary>Active categories with an active mapping to the given city (SRS 12.9.2), ordered for display.</summary>
    Task<IReadOnlyList<Category>> ListServiceableInCityAsync(Guid cityId);

    /// <summary>Active categories whose name contains the query, case-insensitively (SRS 24.3 search).</summary>
    Task<IReadOnlyList<Category>> SearchActiveAsync(string query);
}
