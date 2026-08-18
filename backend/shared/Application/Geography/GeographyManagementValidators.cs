using FluentValidation;

namespace Nestly.Application.Geography;

public class StateCreateRequestValidator : AbstractValidator<StateCreateRequest>
{
    public StateCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10);
    }
}

public class StateUpdateRequestValidator : AbstractValidator<StateUpdateRequest>
{
    public StateUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class CityCreateRequestValidator : AbstractValidator<CityCreateRequest>
{
    public CityCreateRequestValidator()
    {
        RuleFor(x => x.StateId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class CityUpdateRequestValidator : AbstractValidator<CityUpdateRequest>
{
    public CityUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class ZoneCreateRequestValidator : AbstractValidator<ZoneCreateRequest>
{
    public ZoneCreateRequestValidator()
    {
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class ZoneUpdateRequestValidator : AbstractValidator<ZoneUpdateRequest>
{
    public ZoneUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class LocalityCreateRequestValidator : AbstractValidator<LocalityCreateRequest>
{
    public LocalityCreateRequestValidator()
    {
        RuleFor(x => x.ZoneId).NotEmpty();
        RuleFor(x => x.PincodeId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class LocalityUpdateRequestValidator : AbstractValidator<LocalityUpdateRequest>
{
    public LocalityUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

/// <remarks>
/// There is no matching update validator because there is no update: the only
/// mutations <c>IGeographyManagementService</c> exposes for a pincode are
/// create and activate/deactivate, and the latter two take an id, never a code.
/// A master pincode's code is therefore write-once, which is what makes this
/// the single gate the rule below has to hold.
/// </remarks>
public class PincodeCreateRequestValidator : AbstractValidator<PincodeCreateRequest>
{
    public PincodeCreateRequestValidator()
    {
        RuleFor(x => x.CityId).NotEmpty();

        // Task 360: was NotEmpty().MaximumLength(10), which let an admin create
        // a master pincode no customer could ever address-match - task 334
        // pinned customer addresses (and, before it, ProfileValidators) to
        // ^\d{6}$, so a 10-character master row would be serviceable on paper
        // and unreachable in practice. Same rule, same message: a user must not
        // meet two different explanations of one rule depending on which screen
        // they are on.
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(@"^\d{6}$").WithMessage("Pincode must be 6 digits");
    }
}
