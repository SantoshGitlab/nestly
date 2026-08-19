using FluentValidation;

namespace Nestly.Application.ProviderIdentity;

public class ForgotProviderPasswordRequestValidator : AbstractValidator<ForgotProviderPasswordRequest>
{
    public ForgotProviderPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address");
    }
}

public class ResetProviderPasswordRequestValidator : AbstractValidator<ResetProviderPasswordRequest>
{
    public ResetProviderPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("OTP code is required")
            .Matches(@"^\d{6}$").WithMessage("OTP code must be 6 digits");

        // Same floor as registration (RegisterProviderRequestValidator) — a
        // reset must not be a way to set a weaker password than signup allows.
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(128).WithMessage("Password must be at most 128 characters");
    }
}
