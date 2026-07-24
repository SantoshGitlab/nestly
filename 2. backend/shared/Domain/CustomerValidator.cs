using System;
using System.Collections.Generic;
using FluentValidation;
using backend.shared.Application.Domain;

namespace backend.shared.Infrastructure.Persistence.Validators
{
    public class CustomerValidator : AbstractValidator<Customer>
    {
        private readonly IRepository<Customer> _customerRepository;

        public CustomerValidator(IRepository<Customer> customerRepository)
        {
            _customerRepository = customerRepository;
            RuleFor(c => c.Mobile).UniqueMobile(_customerRepository);
            RuleFor(c => c.Email).UniqueEmail(_customerRepository);
        }

        private bool UniqueMobile(Customer customer, string mobile)
        {
            // ... rest of the code
        }

        private bool UniqueEmail(Customer customer, string email)
        {
            // ... rest of the code
        }
    }
}
