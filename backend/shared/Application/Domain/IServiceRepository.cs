using Nestly.Domain;

namespace Nestly.Application;

public interface IServiceRepository : IRepository<Service>
{
    /// <summary>Active services under a category, ordered for display.</summary>
    Task<IReadOnlyList<Service>> ListActiveByCategoryAsync(Guid categoryId);

    Task<Service?> GetBySlugAsync(string slug);

    /// <summary>Active services whose name contains the query, case-insensitively (SRS 24.3 search).</summary>
    Task<IReadOnlyList<Service>> SearchActiveAsync(string query);
}
