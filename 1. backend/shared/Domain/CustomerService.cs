using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using backend.shared.Infrastructure.Persistence.Repositories;
using backend.shared.Application.Domain;
using FluentValidation;

namespace backend.shared.Infrastructure.Persistence.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly NestlyDbContext _context;
        private readonly ICustomerRepository _customerRepository;
        private readonly IValidator<Customer> _customerValidator;

        public CustomerService(NestlyDbContext context, ICustomerRepository customerRepository, IValidator<Customer> customerValidator)
        {
            _context = context;
            _customerRepository = customerRepository;
            _customerValidator = customerValidator;
        }

        // ... rest of the code
    }
}
