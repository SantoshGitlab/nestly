using FluentValidation;

namespace Nestly.Application.Addresses;

public class UpsertAddressRequestValidator : AbstractValidator<UpsertAddressRequest>
{
    public UpsertAddressRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Line2).MaximumLength(300);
        RuleFor(x => x.Landmark).MaximumLength(200);
        // Task 334: was NotEmpty().MaximumLength(12), while customer-web's
        // AddressForm has always enforced ^\d{6}$ - so the two ends disagreed
        // about what a pincode is, and anything the frontend rejected could
        // still reach the API through any other client. Tightened rather than
        // relaxed because six digits is the real Indian format, and because
        // ProfileValidators already enforces exactly this rule with exactly
        // this message - AddressValidators was the lone outlier, not the
        // standard.
        RuleFor(x => x.Pincode)
            .NotEmpty()
            .Matches(@"^\d{6}$").WithMessage("Pincode must be 6 digits");
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m);
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactMobile)
            .NotEmpty()
            .Matches(@"^\+?[1-9]\d{7,14}$").WithMessage("Contact mobile must be a valid phone number");
    }
}
