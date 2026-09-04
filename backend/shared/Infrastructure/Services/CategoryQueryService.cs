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
    private readonly IServiceGroupRepository _serviceGroupRepository;
    private readonly ICategoryGroupRepository _categoryGroupRepository;
    private readonly IServiceabilityRepository _serviceabilityRepository;
    private readonly ICacheService _cache;

    public CategoryQueryService(
        ICategoryRepository categoryRepository,
        IServiceRepository serviceRepository,
        IServiceAddOnRepository addOnRepository,
        IServiceGroupRepository serviceGroupRepository,
        ICategoryGroupRepository categoryGroupRepository,
        IServiceabilityRepository serviceabilityRepository,
        ICacheService cache)
    {
        _categoryRepository = categoryRepository;
        _serviceRepository = serviceRepository;
        _addOnRepository = addOnRepository;
        _serviceGroupRepository = serviceGroupRepository;
        _categoryGroupRepository = categoryGroupRepository;
        _serviceabilityRepository = serviceabilityRepository;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<CategorySummaryResponse>>> ListServiceableInCityAsync(Guid cityId, Guid? pincodeId = null)
    {
        if (!await _serviceabilityRepository.CityExistsAsync(cityId))
        {
            return Error.NotFound("Catalog.CityNotFound", "The specified city does not exist.");
        }

        if (pincodeId is not null && !await _serviceabilityRepository.PincodeExistsAsync(pincodeId.Value))
        {
            return Error.NotFound("Catalog.PincodeNotFound", "The specified pincode does not exist.");
        }

        IReadOnlyList<CategorySummaryResponse> response = await _cache.GetOrCreateAsync(
            CacheKeys.CategoriesInCity(cityId, pincodeId),
            async _ =>
            {
                var categories = await _categoryRepository.ListServiceableInCityAsync(cityId, pincodeId);
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
        if (category is null || !await IsVisibleInHierarchyAsync(category))
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

                // Appliance/Service Group catalog redesign: split the
                // already-fetched, already-ordered service list by
                // ServiceGroupId rather than a second query - one list
                // fetched above serves both the ungrouped Services field and
                // each group's members.
                var ungroupedServices = services.Where(s => s.ServiceGroupId is null).ToList();
                var servicesByGroup = services.Where(s => s.ServiceGroupId is not null)
                    .GroupBy(s => s.ServiceGroupId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var serviceResponses = ungroupedServices.Select(s => ToServiceSummary(s, addOnsByService)).ToList();

                var groups = await _serviceGroupRepository.ListActiveByCategoryIdsAsync([category.Id]);
                var serviceGroupResponses = groups
                    .Where(g => servicesByGroup.ContainsKey(g.Id))
                    .Select(g => new ServiceGroupSummaryResponse(
                        g.Id, g.Name, servicesByGroup[g.Id].Select(s => ToServiceSummary(s, addOnsByService)).ToList()))
                    .ToList();

                // Same split-by-group-id pattern as services/ServiceGroups
                // above, one taxonomy level up: one already-fetched,
                // already-ordered subcategory list serves both the ungrouped
                // Subcategories field and each group's members.
                var subcategories = await _categoryRepository.ListChildrenAsync(category.Id);
                var ungroupedSubcategories = subcategories.Where(c => c.CategoryGroupId is null).ToList();
                var subcategoriesByGroup = subcategories.Where(c => c.CategoryGroupId is not null)
                    .GroupBy(c => c.CategoryGroupId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var categoryGroups = await _categoryGroupRepository.ListActiveByCategoryIdsAsync([category.Id]);
                var categoryGroupResponses = categoryGroups
                    .Where(g => subcategoriesByGroup.ContainsKey(g.Id))
                    .Select(g => new CategoryGroupSummaryResponse(
                        g.Id, g.Name, subcategoriesByGroup[g.Id].Select(ToSummary).ToList()))
                    .ToList();

                return new CategoryDetailResponse(
                    category.Id,
                    category.Name,
                    category.Slug,
                    category.Description,
                    category.IconUrl,
                    category.BannerUrl,
                    category.PageBannerUrl,
                    serviceResponses,
                    ungroupedSubcategories.Select(ToSummary).ToList(),
                    serviceGroupResponses,
                    categoryGroupResponses);
            },
            DetailTtl);

        return detail;
    }

    /// <summary>
    /// Whether <paramref name="category"/> and every one of its ancestor
    /// categories are active (SRS 11.5.4, 12.5.3) - deactivating a parent
    /// hides its sub-categories and their services from customer-facing
    /// discovery even while a sub-category's own active flag stays true.
    /// Bounded to 32 hops so malformed/cyclical parent data can't spin this
    /// into an infinite walk (a true cycle is already rejected at write
    /// time by <c>CategoryManagementService</c>, so this is a defensive
    /// ceiling, not an expected path).
    /// </summary>
    private async Task<bool> IsVisibleInHierarchyAsync(Category category)
    {
        Category? current = category;
        for (var hop = 0; hop < 32 && current is not null; hop++)
        {
            if (!current.IsActive)
            {
                return false;
            }

            if (current.ParentCategoryId is null)
            {
                return true;
            }

            current = await _categoryRepository.GetByIdAsync(current.ParentCategoryId.Value);
        }

        return false;
    }

    private static ServiceSummaryResponse ToServiceSummary(
        Service service, IReadOnlyDictionary<Guid, IReadOnlyList<ServiceAddOn>> addOnsByService)
    {
        var addOns = addOnsByService.TryGetValue(service.Id, out var found) ? found : [];
        return new ServiceSummaryResponse(
            service.Id,
            service.Name,
            service.Slug,
            service.Description,
            service.Price,
            // Ungrouped only (Phase 3 catalog redesign) - grouped add-ons
            // aren't shown at this summary level.
            addOns.Where(a => a.GroupId is null).Select(ToAddOnSummary).ToList(),
            service.CoverImageUrl,
            service.DurationMinutes);
    }

    private static CategorySummaryResponse ToSummary(Category category) => new(
        category.Id,
        category.Name,
        category.Slug,
        category.IconUrl,
        category.BannerUrl,
        category.PageBannerUrl,
        category.IsFeatured);

    private static ServiceAddOnSummaryResponse ToAddOnSummary(ServiceAddOn addOn) => new(
        addOn.Id,
        addOn.Name,
        addOn.Description,
        addOn.Price);
}
