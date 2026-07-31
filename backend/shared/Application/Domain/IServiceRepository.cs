using Nestly.Domain;

namespace Nestly.Application;

public interface IServiceRepository : IRepository<Service>
{
    /// <summary>Active services under a category, ordered for display.</summary>
    Task<IReadOnlyList<Service>> ListActiveByCategoryAsync(Guid categoryId);

    Task<Service?> GetBySlugAsync(string slug);

    /// <summary>Whether any service (other than <paramref name="excludeId"/>, when updating) already uses this slug.</summary>
    Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null);

    /// <summary>Active services whose name contains the query, case-insensitively (SRS 24.3 search).</summary>
    Task<IReadOnlyList<Service>> SearchActiveAsync(string query);

    /// <summary>
    /// Every service regardless of active status, optionally filtered to one
    /// category, ordered for the admin management screen (SRS 12.6.1).
    /// </summary>
    Task<IReadOnlyList<Service>> ListAllAsync(Guid? categoryId);

    /// <summary>Every service regardless of category or active status, for the admin base-price management screen (SRS 12.8.1).</summary>
    Task<IReadOnlyList<Service>> ListAllAsync();
}
