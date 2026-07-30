using Nestly.Application;
using Nestly.Application.Catalog;
using Nestly.Application.Serviceability;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Public category catalog queries (task 41).</summary>
public class CategoryQueryService : ICategoryQueryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddOnRepository _addOnRepository;
    private readonly IServiceabilityRepository _serviceabilityRepository;

    public CategoryQueryService(
        ICategoryRepository categoryRepository,
        IServiceRepository serviceRepository,
        IServiceAddOnRepository addOnRepository,
        IServiceabilityRepository serviceabilityRepository)
    {
        _categoryRepository = categoryRepository;
        _serviceRepository = serviceRepository;
        _addOnRepository = addOnRepository;
        _serviceabilityRepository = serviceabilityRepository;
    }

    public async Task<Result<IReadOnlyList<CategorySummaryResponse>>> ListServiceableInCityAsync(Guid cityId)
    {
        if (!await _serviceabilityRepository.CityExistsAsync(cityId))
        {
            return Error.NotFound("Catalog.CityNotFound", "The specified city does not exist.");
        }

        var categories = await _categoryRepository.ListServiceableInCityAsync(cityId);

        IReadOnlyList<CategorySummaryResponse> response = categories
            .Select(ToSummary)
            .ToList();

        return Result.Success(response);
    }

    public async Task<Result<CategoryDetailResponse>> GetDetailBySlugAsync(string slug)
    {
        var category = await _categoryRepository.GetBySlugAsync(slug);
        if (category is null || !category.IsActive)
        {
            return Error.NotFound("Catalog.CategoryNotFound", "The specified category does not exist.");
        }

        var services = await _serviceRepository.ListActiveByCategoryAsync(category.Id);
        var serviceResponses = new List<ServiceSummaryResponse>(services.Count);

        foreach (var service in services)
        {
            var addOns = await _addOnRepository.ListActiveByServiceAsync(service.Id);
            serviceResponses.Add(new ServiceSummaryResponse(
                service.Id,
                service.Name,
                service.Slug,
                service.Description,
                service.Price,
                addOns.Select(ToAddOnSummary).ToList()));
        }

        return new CategoryDetailResponse(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.IconUrl,
            category.BannerUrl,
            serviceResponses);
    }

    private static CategorySummaryResponse ToSummary(Category category) => new(
        category.Id,
        category.Name,
        category.Slug,
        category.IconUrl,
        category.BannerUrl,
        category.IsFeatured);

    private static ServiceAddOnSummaryResponse ToAddOnSummary(ServiceAddOn addOn) => new(
        addOn.Id,
        addOn.Name,
        addOn.Description,
        addOn.Price);
}
