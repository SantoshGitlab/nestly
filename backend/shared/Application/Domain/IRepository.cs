using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Application;

public interface IRepository<T> where T : Entity<Guid>
{
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task<T?> GetByIdAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
}
