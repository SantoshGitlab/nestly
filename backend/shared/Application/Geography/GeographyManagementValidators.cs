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

public class PincodeCreateRequestValidator : AbstractValidator<PincodeCreateRequest>
{
    public PincodeCreateRequestValidator()
    {
        RuleFor(x => x.CityId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10);
    }
}
