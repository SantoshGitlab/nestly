using FluentValidation;

namespace Nestly.Application.Profile;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200);

        // Matches the customer table's column widths so an over-long value is
        // rejected with a 400 rather than surfacing as a database error.
        RuleFor(x => x.City).MaximumLength(200);
        RuleFor(x => x.State).MaximumLength(200);
        RuleFor(x => x.Country).MaximumLength(200);

        RuleFor(x => x.Pincode)
            .Matches(@"^\d{6}$").WithMessage("Pincode must be 6 digits")
            .When(x => !string.IsNullOrWhiteSpace(x.Pincode));

        RuleFor(x => x.DateOfBirth)
            .LessThan(_ => DateTime.UtcNow.Date).WithMessage("Date of birth must be in the past")
            .When(x => x.DateOfBirth.HasValue);
    }
}

public class RequestMobileChangeOtpRequestValidator : AbstractValidator<RequestMobileChangeOtpRequest>
{
    public RequestMobileChangeOtpRequestValidator()
    {
        RuleFor(x => x.NewMobile)
            .NotEmpty().WithMessage("Mobile number is required")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Mobile number must be a valid phone number");
    }
}

public class ConfirmMobileChangeRequestValidator : AbstractValidator<ConfirmMobileChangeRequest>
{
    public ConfirmMobileChangeRequestValidator()
    {
        RuleFor(x => x.NewMobile)
            .NotEmpty().WithMessage("Mobile number is required")
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Mobile number must be a valid phone number");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("OTP code is required")
            .Matches(@"^\d{6}$").WithMessage("OTP code must be 6 digits");
    }
}

public class RequestEmailChangeOtpRequestValidator : AbstractValidator<RequestEmailChangeOtpRequest>
{
    public RequestEmailChangeOtpRequestValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(200);
    }
}

public class ConfirmEmailChangeRequestValidator : AbstractValidator<ConfirmEmailChangeRequest>
{
    public ConfirmEmailChangeRequestValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(200);

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("OTP code is required")
            .Matches(@"^\d{6}$").WithMessage("OTP code must be 6 digits");
    }
}
