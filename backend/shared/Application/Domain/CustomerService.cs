using Nestly.BuildingBlocks.Primitives;
using Nestly.BuildingBlocks.Results;

namespace Nestly.Application;

public interface ICustomerService
{
    Task<Customer?> GetByIdAsync(Guid id);
    Task<Result<Customer>> RegisterAsync(Customer customer);
    Task UpdateAsync(Customer customer);
    Task DeleteAsync(Customer customer);
    Task<Result> UpdateStatusAsync(Guid customerId, CustomerStatus newStatus);
}

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;

    public CustomerService(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public Task<Customer?> GetByIdAsync(Guid id) => _customerRepository.GetByIdAsync(id);

    /// <summary>
    /// Enforces the account rule that mobile and email each identify at most
    /// one customer (SRS 23.1). This is the domain-level check; the unique
    /// indexes on customer.mobile/email are the last line of defense against
    /// a race between the check and the insert, not the primary one.
    /// </summary>
    public async Task<Result<Customer>> RegisterAsync(Customer customer)
    {
        if (await _customerRepository.ExistsByMobileAsync(customer.Mobile))
        {
            return Error.Conflict("Customer.DuplicateMobile", "A customer with this mobile number already exists.");
        }

        if (!string.IsNullOrWhiteSpace(customer.Email) && await _customerRepository.ExistsByEmailAsync(customer.Email))
        {
            return Error.Conflict("Customer.DuplicateEmail", "A customer with this email already exists.");
        }

        await _customerRepository.AddAsync(customer);
        return customer;
    }

    public Task UpdateAsync(Customer customer) => _customerRepository.UpdateAsync(customer);
    public Task DeleteAsync(Customer customer) => _customerRepository.DeleteAsync(customer);

    public async Task<Result> UpdateStatusAsync(Guid customerId, CustomerStatus newStatus)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer is null)
        {
            return Result.Failure(Error.NotFound("Customer.NotFound", $"Customer {customerId} was not found."));
        }

        customer.UpdateStatus(newStatus);
        await _customerRepository.UpdateAsync(customer);
        return Result.Success();
    }
}
