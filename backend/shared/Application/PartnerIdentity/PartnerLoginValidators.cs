using FluentValidation;

namespace Nestly.Application.PartnerIdentity;

public class RequestPartnerLoginOtpRequestValidator : AbstractValidator<RequestPartnerLoginOtpRequest>
{
    public RequestPartnerLoginOtpRequestValidator()
    {
        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile number is required")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Mobile number must be a valid phone number");
    }
}

public class LoginPartnerWithOtpRequestValidator : AbstractValidator<LoginPartnerWithOtpRequest>
{
    public LoginPartnerWithOtpRequestValidator()
    {
        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile number is required")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Mobile number must be a valid phone number");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("OTP code is required")
            .Matches(@"^\d{6}$").WithMessage("OTP code must be 6 digits");
    }
}

public class RefreshPartnerTokenRequestValidator : AbstractValidator<RefreshPartnerTokenRequest>
{
    public RefreshPartnerTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class LogoutPartnerRequestValidator : AbstractValidator<LogoutPartnerRequest>
{
    public LogoutPartnerRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
