using FluentValidation;

namespace Nestly.Application.PartnerAvailability;

public class PartnerAvailabilityWindowInputValidator : AbstractValidator<PartnerAvailabilityWindowInput>
{
    public PartnerAvailabilityWindowInputValidator()
    {
        RuleFor(x => x.DayOfWeek).IsInEnum();
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("The start time must be before the end time.");
    }
}

public class UpdatePartnerAvailabilityWindowsRequestValidator : AbstractValidator<UpdatePartnerAvailabilityWindowsRequest>
{
    public UpdatePartnerAvailabilityWindowsRequestValidator()
    {
        RuleForEach(x => x.Windows).SetValidator(new PartnerAvailabilityWindowInputValidator());
    }
}

public class AddPartnerBlackoutDateRequestValidator : AbstractValidator<AddPartnerBlackoutDateRequest>
{
    public AddPartnerBlackoutDateRequestValidator()
    {
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("The start date must not be after the end date.");

        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
