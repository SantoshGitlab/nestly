using Nestly.Domain;

namespace Nestly.Application;

public interface ICustomerAddressRepository
{
    Task AddAsync(CustomerAddress entity);
    Task UpdateAsync(CustomerAddress entity);
    Task DeleteAsync(CustomerAddress entity);
    Task<CustomerAddress?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(Guid customerId);
    Task<CustomerAddress?> GetDefaultAsync(Guid customerId);
}
