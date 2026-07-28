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
        RuleFor(x => x.Pincode).NotEmpty().MaximumLength(12);
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
