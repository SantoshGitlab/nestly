using FluentValidation;

namespace Nestly.Application.Identity;

public class RequestLoginOtpRequestValidator : AbstractValidator<RequestLoginOtpRequest>
{
    public RequestLoginOtpRequestValidator()
    {
        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile number is required")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Mobile number must be a valid phone number");
    }
}

public class LoginWithOtpRequestValidator : AbstractValidator<LoginWithOtpRequest>
{
    public LoginWithOtpRequestValidator()
    {
        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile number is required")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Mobile number must be a valid phone number");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("OTP code is required")
            .Matches(@"^\d{6}$").WithMessage("OTP code must be 6 digits");
    }
}

public class LoginWithPasswordRequestValidator : AbstractValidator<LoginWithPasswordRequest>
{
    public LoginWithPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
