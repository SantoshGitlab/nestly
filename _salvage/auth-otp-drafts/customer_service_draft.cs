using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using backend.shared.Application.Domain;
using backend.shared.Infrastructure.Persistence.Configurations;

namespace backend.shared.Application.Domain
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IOTPService _otpService;
        private readonly IRateLimitingService _rateLimitingService;

        public CustomerService(ICustomerRepository customerRepository, IOTPService otpService, IRateLimitingService rateLimitingService)
        {
            _customerRepository = customerRepository;
            _otpService = otpService;
            _rateLimitingService = rateLimitingService;
        }

        // ... rest of the code
    }
}
