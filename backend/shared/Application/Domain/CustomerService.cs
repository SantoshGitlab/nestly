using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using backend.shared.Application.Domain;
using backend.shared.Infrastructure.Persistence;

namespace backend.shared.Application.Domain
{
    public class CustomerService : ICustomerService
    {
        private readonly IRepository<Customer> _customerRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CustomerService(IRepository<Customer> customerRepository, IUnitOfWork unitOfWork)
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
