using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Application;

public interface ICustomerService
{
    Task<Customer?> GetByIdAsync(Guid id);
    Task AddAsync(Customer customer);
    Task UpdateAsync(Customer customer);
    Task DeleteAsync(Customer customer);
    Task UpdateStatusAsync(Guid customerId, CustomerStatus newStatus);
}

public class CustomerService : ICustomerService
{
    private readonly IRepository<Customer> _customerRepository;

    public CustomerService(IRepository<Customer> customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public Task<Customer?> GetByIdAsync(Guid id) => _customerRepository.GetByIdAsync(id);
    public Task AddAsync(Customer customer) => _customerRepository.AddAsync(customer);
    public Task UpdateAsync(Customer customer) => _customerRepository.UpdateAsync(customer);
    public Task DeleteAsync(Customer customer) => _customerRepository.DeleteAsync(customer);

    public async Task UpdateStatusAsync(Guid customerId, CustomerStatus newStatus)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId)
            ?? throw new Exception($"Customer {customerId} not found.");
        customer.UpdateStatus(newStatus);
        await _customerRepository.UpdateAsync(customer);
    }
}
