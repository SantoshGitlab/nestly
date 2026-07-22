using System.Threading.Tasks;

namespace backend.shared.Application.Domain
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IValidator<Customer> _validator;

        public CustomerService(ICustomerRepository customerRepository, IValidator<Customer> validator)
        {
            _customerRepository = customerRepository;
            _validator = validator;
        }

        public async Task CreateAsync(Customer customer)
        {
            var validationResult = await _validator.ValidateAsync(customer);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            if (await _customerRepository.ExistsByMobileAsync(customer.Mobile))
            {
                throw new InvalidOperationException("Mobile number already exists");
            }

            if (await _customerRepository.ExistsByEmailAsync(customer.Email))
            {
                throw new InvalidOperationException("Email address already exists");
            }

            await _customerRepository.AddAsync(customer);
        }

        public async Task UpdateAsync(Customer customer)
        {
            var validationResult = await _validator.ValidateAsync(customer);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            await _customerRepository.UpdateAsync(customer);
        }

        public async Task DeleteAsync(Guid id)
        {
            var customer = await _customerRepository.GetByIdAsync(id);
            if (customer == null)
            {
                throw new InvalidOperationException("Customer not found");
            }

            await _customerRepository.DeleteAsync(customer);
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
