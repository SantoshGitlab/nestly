using Nestly.Application;
using Nestly.Application.Abstractions.Caching;
using Nestly.Application.Catalog;
using Nestly.Application.Serviceability;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Public category catalog queries (task 41), read-cached per task 49.</summary>
public class CategoryQueryService : ICategoryQueryService
{
    private static readonly TimeSpan DetailTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ListTtl = TimeSpan.FromMinutes(5);

    private readonly ICategoryRepository _categoryRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddOnRepository _addOnRepository;
    private readonly IServiceabilityRepository _serviceabilityRepository;
    private readonly ICacheService _cache;

    public CategoryQueryService(
        ICategoryRepository categoryRepository,
        IServiceRepository serviceRepository,
        IServiceAddOnRepository addOnRepository,
        IServiceabilityRepository serviceabilityRepository,
        ICacheService cache)
    {
        _categoryRepository = categoryRepository;
        _serviceRepository = serviceRepository;
        _addOnRepository = addOnRepository;
        _serviceabilityRepository = serviceabilityRepository;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<CategorySummaryResponse>>> ListServiceableInCityAsync(Guid cityId)
    {
        if (!await _serviceabilityRepository.CityExistsAsync(cityId))
        {
            return Error.NotFound("Catalog.CityNotFound", "The specified city does not exist.");
        }

        IReadOnlyList<CategorySummaryResponse> response = await _cache.GetOrCreateAsync(
            CacheKeys.CategoriesInCity(cityId),
            async _ =>
            {
                var categories = await _categoryRepository.ListServiceableInCityAsync(cityId);
                return (IReadOnlyList<CategorySummaryResponse>)categories.Select(ToSummary).ToList();
            },
            ListTtl);

        return Result.Success(response);
    }

    public async Task<Result<CategoryDetailResponse>> GetDetailBySlugAsync(string slug)
    {
        // The slug -> id lookup is a cheap unique-index hit and always fresh;
        // only the expensive nested services+add-ons assembly below is cached.
        var category = await _categoryRepository.GetBySlugAsync(slug);
        if (category is null || !category.IsActive)
        {
            return Error.NotFound("Catalog.CategoryNotFound", "The specified category does not exist.");
        }

        var detail = await _cache.GetOrCreateAsync(
            CacheKeys.Category(category.Id),
            async _ =>
            {
                var services = await _serviceRepository.ListActiveByCategoryAsync(category.Id);

                // Batched (task 136a fix): this used to call
                // ListActiveByServiceAsync once per service inside the loop
                // below - an N+1 that fired on every cache miss for a
                // category detail page. One query for every service's
                // add-ons, then grouped in memory, replaces N.
                var addOnsByService = (await _addOnRepository.ListActiveByServiceIdsAsync(services.Select(s => s.Id).ToList()))
                    .GroupBy(a => a.ServiceId)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<ServiceAddOn>)g.ToList());

                var serviceResponses = new List<ServiceSummaryResponse>(services.Count);

                foreach (var service in services)
                {
                    var addOns = addOnsByService.TryGetValue(service.Id, out var found) ? found : [];
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
            },
            DetailTtl);

        return detail;
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
