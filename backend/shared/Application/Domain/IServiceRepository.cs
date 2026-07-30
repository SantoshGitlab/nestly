using Nestly.Domain;

namespace Nestly.Application;

public interface IServiceRepository : IRepository<Service>
{
    /// <summary>Active services under a category, ordered for display.</summary>
    Task<IReadOnlyList<Service>> ListActiveByCategoryAsync(Guid categoryId);
}
