using FluentValidation;

namespace backend.shared.Application.Domain
{
    public class CustomerValidator : AbstractValidator<Customer>
    {
        public CustomerValidator()
        {
            RuleFor(c => c.Mobile).NotEmpty().WithMessage("Mobile number is required");
            RuleFor(c => c.Email).NotEmpty().WithMessage("Email address is required");
            RuleFor(c => c.Name).NotEmpty().WithMessage("Name is required");
            RuleFor(c => c.DateOfBirth).NotEmpty().WithMessage("Date of birth is required");
            RuleFor(c => c.Address).NotEmpty().WithMessage("Address is required");
            RuleFor(c => c.City).NotEmpty().WithMessage("City is required");
            RuleFor(c => c.State).NotEmpty().WithMessage("State is required");
            RuleFor(c => c.Pincode).NotEmpty().WithMessage("Pincode is required");
            RuleFor(c => c.Country).NotEmpty().WithMessage("Country is required");

            RuleFor(c => c.Mobile)
                .Must(UniqueMobile)
                .WithMessage("Mobile number must be unique");

            RuleFor(c => c.Email)
                .Must(UniqueEmail)
                .WithMessage("Email address must be unique");
        }

        private bool UniqueMobile(Customer customer, string mobile)
        {
            // Implement uniqueness check for mobile
            return true;
        }

        private bool UniqueEmail(Customer customer, string email)
        {
            // Implement uniqueness check for email
            return true;
        }
    }
}
