using FluentValidation;

namespace Nestly.Application.Serviceability;

public class CategoryCityMappingCreateRequestValidator : AbstractValidator<CategoryCityMappingCreateRequest>
{
    public CategoryCityMappingCreateRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.CityId).NotEmpty();
    }
}

public class ServicePincodeMappingCreateRequestValidator : AbstractValidator<ServicePincodeMappingCreateRequest>
{
    public ServicePincodeMappingCreateRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.PincodeId).NotEmpty();
    }
}
