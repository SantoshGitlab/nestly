using FluentValidation;

namespace Nestly.Application.Identity;

public class RequestRegistrationOtpRequestValidator : AbstractValidator<RequestRegistrationOtpRequest>
{
    public RequestRegistrationOtpRequestValidator()
    {
        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile number is required")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Mobile number must be a valid phone number");
    }
}

public class RegisterCustomerRequestValidator : AbstractValidator<RegisterCustomerRequest>
{
    public RegisterCustomerRequestValidator()
    {
        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile number is required")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Mobile number must be a valid phone number");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("OTP code is required")
            .Matches(@"^\d{6}$").WithMessage("OTP code must be 6 digits");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Password)
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .When(x => !string.IsNullOrEmpty(x.Password));

        RuleFor(x => x.ConsentAccepted)
            .Equal(true).WithMessage("Consent to Terms & Privacy is required");

        RuleFor(x => x.ReferralCode)
            .MaximumLength(20).WithMessage("Referral code is not valid")
            .When(x => !string.IsNullOrWhiteSpace(x.ReferralCode));
    }
}
