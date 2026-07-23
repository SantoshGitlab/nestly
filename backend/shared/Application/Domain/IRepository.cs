using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using backend.shared.Domain;

namespace backend.shared.Application.Domain
{
    public interface IRepository<T> where T : Entity<Guid>
    {
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task<T> GetByIdAsync(Guid id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<bool> ExistsAsync(Guid id);
        Task<IEnumerable<IDomainEvent>> GetDomainEventsByIdAsync(Guid id);
    }
}
