using FluentValidation;

namespace Nestly.Application.ProviderProfile;

public class UpdateProviderProfileRequestValidator : AbstractValidator<UpdateProviderProfileRequest>
{
    public UpdateProviderProfileRequestValidator()
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

/// <summary>
/// Task 293. The scheme check is the security-relevant rule, not the length
/// one: this value ends up in an <c>img src</c> on a customer's screen, so a
/// <c>javascript:</c> or <c>data:</c> reference there is script execution
/// rather than a picture. <c>Provider.SubmitPhoto</c> enforces the same rule
/// in the domain - this validator exists so a bad value comes back as a 400
/// with a usable message instead of an unhandled ArgumentException.
/// </summary>
public class UpdateProviderPhotoRequestValidator : AbstractValidator<UpdateProviderPhotoRequest>
{
    private const int MaxPhotoUrlLength = 2000;

    public UpdateProviderPhotoRequestValidator()
    {
        // Null/empty is the documented "remove my photo" case, so every rule
        // below applies only when a value was actually supplied.
        When(x => !string.IsNullOrWhiteSpace(x.PhotoUrl), () =>
        {
            RuleFor(x => x.PhotoUrl!)
                .MaximumLength(MaxPhotoUrlLength)
                .Must(BeAnAbsoluteHttpUrl)
                .WithMessage("A photo must be an absolute http or https URL");
        });
    }

    private static bool BeAnAbsoluteHttpUrl(string photoUrl) =>
        Uri.TryCreate(photoUrl.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public class ProviderServiceAreaInputValidator : AbstractValidator<ProviderServiceAreaInput>
{
    public ProviderServiceAreaInputValidator()
    {
        RuleFor(x => x.CityId).NotEmpty().WithMessage("A city is required for each service area");
    }
}

public class UpdateProviderServiceAreasRequestValidator : AbstractValidator<UpdateProviderServiceAreasRequest>
{
    public UpdateProviderServiceAreasRequestValidator()
    {
        RuleForEach(x => x.Areas).SetValidator(new ProviderServiceAreaInputValidator());
    }
}

public class ProviderSkillInputValidator : AbstractValidator<ProviderSkillInput>
{
    public ProviderSkillInputValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("A category is required for each skill");
    }
}

public class UpdateProviderSkillsRequestValidator : AbstractValidator<UpdateProviderSkillsRequest>
{
    public UpdateProviderSkillsRequestValidator()
    {
        RuleForEach(x => x.Skills).SetValidator(new ProviderSkillInputValidator());
    }
}
