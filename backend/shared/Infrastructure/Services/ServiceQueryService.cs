using Nestly.Application;
using Nestly.Application.Abstractions.Caching;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Public service catalog queries (tasks 42a/42b), read-cached per task 49.</summary>
public class ServiceQueryService : IServiceQueryService
{
    private static readonly TimeSpan DetailTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ListTtl = TimeSpan.FromMinutes(15);

    private readonly ICategoryRepository _categoryRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddOnRepository _addOnRepository;
    private readonly ICacheService _cache;

    public ServiceQueryService(
        ICategoryRepository categoryRepository,
        IServiceRepository serviceRepository,
        IServiceAddOnRepository addOnRepository,
        ICacheService cache)
    {
        _categoryRepository = categoryRepository;
        _serviceRepository = serviceRepository;
        _addOnRepository = addOnRepository;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<ServiceListItemResponse>>> ListByCategoryAsync(Guid categoryId)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId);
        if (category is null || !category.IsActive)
        {
            return Error.NotFound("Catalog.CategoryNotFound", "The specified category does not exist.");
        }

        IReadOnlyList<ServiceListItemResponse> response = await _cache.GetOrCreateAsync(
            CacheKeys.ServicesByCategory(categoryId),
            async _ =>
            {
                var services = await _serviceRepository.ListActiveByCategoryAsync(categoryId);
                return (IReadOnlyList<ServiceListItemResponse>)services.Select(ToListItem).ToList();
            },
            ListTtl);

        return Result.Success(response);
    }

    public async Task<Result<ServiceDetailResponse>> GetDetailBySlugAsync(string slug)
    {
        // Slug -> id is a cheap unique-index hit and always fresh; only the
        // expensive category-breadcrumb + add-ons assembly below is cached.
        var service = await _serviceRepository.GetBySlugAsync(slug);
        if (service is null || !service.IsActive)
        {
            return Error.NotFound("Catalog.ServiceNotFound", "The specified service does not exist.");
        }

        var detail = await _cache.GetOrCreateAsync(
            CacheKeys.Service(service.Id),
            async _ =>
            {
                var category = await _categoryRepository.GetByIdAsync(service.CategoryId);
                var addOns = await _addOnRepository.ListActiveByServiceAsync(service.Id);

                return new ServiceDetailResponse(
                    service.Id,
                    service.Name,
                    service.Slug,
                    service.Description,
                    service.Price,
                    service.Inclusions,
                    service.Exclusions,
                    service.CancellationPolicy,
                    service.ReschedulePolicy,
                    service.CategoryId,
                    category?.Name ?? string.Empty,
                    category?.Slug ?? string.Empty,
                    addOns.Select(ToAddOnSummary).ToList());
            },
            DetailTtl);

        return detail;
    }

    private static ServiceListItemResponse ToListItem(Service service) => new(
        service.Id,
        service.Name,
        service.Slug,
        service.Description,
        service.Price);

    private static ServiceAddOnSummaryResponse ToAddOnSummary(ServiceAddOn addOn) => new(
        addOn.Id,
        addOn.Name,
        addOn.Description,
        addOn.Price);
}
