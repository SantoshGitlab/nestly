using FluentValidation;

namespace Nestly.Application.ProviderIdentity;

public class RequestProviderRegistrationOtpRequestValidator : AbstractValidator<RequestProviderRegistrationOtpRequest>
{
    public RequestProviderRegistrationOtpRequestValidator()
    {
        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile number is required")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Mobile number must be a valid phone number");
    }
}

public class RegisterProviderRequestValidator : AbstractValidator<RegisterProviderRequest>
{
    public RegisterProviderRequestValidator()
    {
        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile number is required")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Mobile number must be a valid phone number");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("OTP code is required")
            .Matches(@"^\d{6}$").WithMessage("OTP code must be 6 digits");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("Legal name is required")
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required")
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Password)
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(128).WithMessage("Password must be at most 128 characters")
            .When(x => !string.IsNullOrEmpty(x.Password));

        RuleFor(x => x.ConsentAccepted)
            .Equal(true).WithMessage("Consent to Terms & Privacy is required");
    }
}

public class RequestProviderRegistrationEmailOtpRequestValidator : AbstractValidator<RequestProviderRegistrationEmailOtpRequest>
{
    public RequestProviderRegistrationEmailOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address");
    }
}

public class RegisterProviderWithEmailRequestValidator : AbstractValidator<RegisterProviderWithEmailRequest>
{
    public RegisterProviderWithEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("OTP code is required")
            .Matches(@"^\d{6}$").WithMessage("OTP code must be 6 digits");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("Legal name is required")
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required")
            .MaximumLength(200);

        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile number is required")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Mobile number must be a valid phone number");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(128).WithMessage("Password must be at most 128 characters");

        RuleFor(x => x.ConsentAccepted)
            .Equal(true).WithMessage("Consent to Terms & Privacy is required");
    }
}
