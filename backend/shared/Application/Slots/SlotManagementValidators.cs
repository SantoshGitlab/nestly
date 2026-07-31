using FluentValidation;

namespace Nestly.Application.Slots;

public class SlotWindowCreateRequestValidator : AbstractValidator<SlotWindowCreateRequest>
{
    public SlotWindowCreateRequestValidator()
    {
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("The start time must be before the end time.");
        RuleFor(x => x.MaxBookingsPerSlot).GreaterThan(0).When(x => x.MaxBookingsPerSlot is not null);
        RuleFor(x => x.DaysOfWeek).NotNull();
        RuleForEach(x => x.DaysOfWeek).IsInEnum();
    }
}

public class SlotWindowUpdateRequestValidator : AbstractValidator<SlotWindowUpdateRequest>
{
    public SlotWindowUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime).WithMessage("The start time must be before the end time.");
        RuleFor(x => x.DaysOfWeek).NotNull();
        RuleForEach(x => x.DaysOfWeek).IsInEnum();
    }
}

public class SlotWindowCapacityUpdateRequestValidator : AbstractValidator<SlotWindowCapacityUpdateRequest>
{
    public SlotWindowCapacityUpdateRequestValidator()
    {
        RuleFor(x => x.MaxBookingsPerSlot).GreaterThan(0).When(x => x.MaxBookingsPerSlot is not null);
    }
}

public class SlotBlackoutCreateRequestValidator : AbstractValidator<SlotBlackoutCreateRequest>
{
    public SlotBlackoutCreateRequestValidator()
    {
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("The start date must not be after the end date.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public class SlotBookingPolicyUpsertRequestValidator : AbstractValidator<SlotBookingPolicyUpsertRequest>
{
    public SlotBookingPolicyUpsertRequestValidator()
    {
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.CutoffMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxAdvanceDays).GreaterThan(0);
    }
}

public class SlotAvailabilityOverrideCreateRequestValidator : AbstractValidator<SlotAvailabilityOverrideCreateRequest>
{
    public SlotAvailabilityOverrideCreateRequestValidator()
    {
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
