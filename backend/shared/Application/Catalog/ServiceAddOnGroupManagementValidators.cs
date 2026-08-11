using FluentValidation;
using Nestly.Domain;

namespace Nestly.Application.Catalog;

public class ServiceAddOnGroupCreateRequestValidator : AbstractValidator<ServiceAddOnGroupCreateRequest>
{
    public ServiceAddOnGroupCreateRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SelectionType).Must(BeAValidSelectionType)
            .WithMessage($"Selection type must be one of: {string.Join(", ", Enum.GetNames<AddOnGroupSelectionType>())}.");
        RuleFor(x => x.MinSelect).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxSelect).GreaterThanOrEqualTo(1).When(x => x.MaxSelect is not null);
        RuleFor(x => x.MaxSelect)
            .GreaterThanOrEqualTo(x => x.MinSelect)
            .When(x => x.MaxSelect is not null)
            .WithMessage("MaxSelect cannot be less than MinSelect.");
        RuleFor(x => x.MaxSelect)
            .Must(max => max is null or 1)
            .When(x => x.SelectionType == nameof(AddOnGroupSelectionType.Single))
            .WithMessage("A Single-selection group's MaxSelect cannot exceed 1.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }

    private static bool BeAValidSelectionType(string value) => Enum.TryParse<AddOnGroupSelectionType>(value, out _);
}

public class ServiceAddOnGroupUpdateRequestValidator : AbstractValidator<ServiceAddOnGroupUpdateRequest>
{
    public ServiceAddOnGroupUpdateRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SelectionType).Must(BeAValidSelectionType)
            .WithMessage($"Selection type must be one of: {string.Join(", ", Enum.GetNames<AddOnGroupSelectionType>())}.");
        RuleFor(x => x.MinSelect).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxSelect).GreaterThanOrEqualTo(1).When(x => x.MaxSelect is not null);
        RuleFor(x => x.MaxSelect)
            .GreaterThanOrEqualTo(x => x.MinSelect)
            .When(x => x.MaxSelect is not null)
            .WithMessage("MaxSelect cannot be less than MinSelect.");
        RuleFor(x => x.MaxSelect)
            .Must(max => max is null or 1)
            .When(x => x.SelectionType == nameof(AddOnGroupSelectionType.Single))
            .WithMessage("A Single-selection group's MaxSelect cannot exceed 1.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }

    private static bool BeAValidSelectionType(string value) => Enum.TryParse<AddOnGroupSelectionType>(value, out _);
}
