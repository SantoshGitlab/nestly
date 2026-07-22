using backend.shared.Application.Domain;

namespace backend.shared.Application.Domain
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IOTPService _otpService;

        public CustomerService(ICustomerRepository customerRepository, IOTPService otpService)
        {
            _customerRepository = customerRepository;
            _otpService = otpService;
        }

        public async Task CreateAsync(Customer customer)
        {
            if (await _customerRepository.ExistsByMobileAsync(customer.Mobile))
            {
                throw new ValidationException("Mobile number already exists.");
            }

            if (await _customerRepository.ExistsByEmailAsync(customer.Email))
            {
                throw new ValidationException("Email address already exists.");
            }

            await _customerRepository.AddAsync(customer);
        }

        public async Task UpdateAsync(Customer customer)
        {
            // Implementation for updating a customer
        }

        public async Task DeleteAsync(Guid id)
        {
            // Implementation for deleting a customer
        }

        public async Task<Customer> GetByIdAsync(Guid id)
        {
            return await _customerRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<Customer>> GetAllAsync()
        {
            return await _customerRepository.GetAllAsync();
        }
    }
}
