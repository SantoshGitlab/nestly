namespace backend.shared.Application.Domain
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CustomerService(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
        {
            _customerRepository = customerRepository;
            _unitOfWork = unitOfWork;
        }

        // ... existing methods ...

        public async Task UpdateStatusAsync(Guid customerId, CustomerStatus newStatus)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);

            if (customer == null)
                throw new Exception("Customer not found.");

            customer.UpdateStatus(newStatus);

            await _unitOfWork.CommitAsync();
        }
    }
}
