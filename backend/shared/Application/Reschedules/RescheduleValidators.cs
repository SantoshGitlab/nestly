using FluentValidation;

namespace Nestly.Application.Reschedules;

public class RescheduleBookingRequestValidator : AbstractValidator<RescheduleBookingRequest>
{
    public RescheduleBookingRequestValidator()
    {
        RuleFor(x => x.LocalityId).NotEmpty();
        RuleFor(x => x.SlotWindowId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500).When(x => x.Reason is not null);
    }
}
