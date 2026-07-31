using FluentValidation;

namespace Nestly.Application.PartnerProfile;

public class UpdatePartnerProfileRequestValidator : AbstractValidator<UpdatePartnerProfileRequest>
{
    public UpdatePartnerProfileRequestValidator()
    {
        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("Legal name is required")
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required")
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class PartnerServiceAreaInputValidator : AbstractValidator<PartnerServiceAreaInput>
{
    public PartnerServiceAreaInputValidator()
    {
        RuleFor(x => x.CityId).NotEmpty().WithMessage("A city is required for each service area");
    }
}

public class UpdatePartnerServiceAreasRequestValidator : AbstractValidator<UpdatePartnerServiceAreasRequest>
{
    public UpdatePartnerServiceAreasRequestValidator()
    {
        RuleForEach(x => x.Areas).SetValidator(new PartnerServiceAreaInputValidator());
    }
}

public class PartnerSkillInputValidator : AbstractValidator<PartnerSkillInput>
{
    public PartnerSkillInputValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("A category is required for each skill");
    }
}

public class UpdatePartnerSkillsRequestValidator : AbstractValidator<UpdatePartnerSkillsRequest>
{
    public UpdatePartnerSkillsRequestValidator()
    {
        RuleForEach(x => x.Skills).SetValidator(new PartnerSkillInputValidator());
    }
}
