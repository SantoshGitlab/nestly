using FluentValidation;
using backend.shared.Application.Domain;

namespace backend.shared.Domain
{
    public class CustomerValidator : AbstractValidator<Customer>
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IOTPService _otpService;

        public CustomerValidator(ICustomerRepository customerRepository, IOTPService otpService)
        {
            _customerRepository = customerRepository;
            _otpService = otpService;

            RuleFor(c => c.Mobile).NotEmpty().WithMessage("Mobile number is required.");
            RuleFor(c => c.Email).NotEmpty().WithMessage("Email address is required.");

            RuleFor(c => c.Mobile).MustAsync(async (mobile, cancellation) =>
                await UniqueMobile(c, mobile)).WithMessage("Mobile number already exists.");

            RuleFor(c => c.Email).MustAsync(async (email, cancellation) =>
                await UniqueEmail(c, email)).WithMessage("Email address already exists.");
        }

        private bool UniqueMobile(Customer customer, string mobile)
        {
            return !_customerRepository.ExistsByMobileAsync(mobile).Result;
        }

        private bool UniqueEmail(Customer customer, string email)
        {
            return !_customerRepository.ExistsByEmailAsync(email).Result;
        }
    }
}
