using FluentValidation;

namespace Nestly.Application.Bookings;

public class BookingSummaryRequestValidator : AbstractValidator<BookingSummaryRequest>
{
    public BookingSummaryRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.AddressId).NotEmpty();
        RuleFor(x => x.LocalityId).NotEmpty();
        RuleFor(x => x.SlotWindowId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleForEach(x => x.AddOns).ChildRules(addOn =>
        {
            addOn.RuleFor(a => a.AddOnId).NotEmpty();
            addOn.RuleFor(a => a.Quantity).GreaterThan(0);
        });
    }
}
