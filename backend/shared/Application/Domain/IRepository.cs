using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Application;

public interface IRepository<T> where T : Entity<Guid>
{
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<bool> ExistsAsync(Guid id);
}
