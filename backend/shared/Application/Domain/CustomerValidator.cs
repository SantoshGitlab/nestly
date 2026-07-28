using FluentValidation;

namespace Nestly.Application
{
    public class CustomerValidator : AbstractValidator<Customer>
    {
        public CustomerValidator()
        {
            // Mobile+OTP is the always-available registration path (SRS
            // 11.2.1); only mobile and name are collected at signup. Email,
            // date of birth, and address are optional/out of scope here (the
            // address book is a separate feature — SRS 11.3).
            RuleFor(c => c.Mobile).NotEmpty().WithMessage("Mobile number is required");
            RuleFor(c => c.Name).NotEmpty().WithMessage("Name is required");

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

        private bool UniqueEmail(Customer customer, string? email)
        {
            // Implement uniqueness check for email
            return true;
        }
    }
}
