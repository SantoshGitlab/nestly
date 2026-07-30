using Nestly.Application;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Public service catalog queries (tasks 42a/42b).</summary>
public class ServiceQueryService : IServiceQueryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddOnRepository _addOnRepository;

    public ServiceQueryService(
        ICategoryRepository categoryRepository,
        IServiceRepository serviceRepository,
        IServiceAddOnRepository addOnRepository)
    {
        _categoryRepository = categoryRepository;
        _serviceRepository = serviceRepository;
        _addOnRepository = addOnRepository;
    }

    public async Task<Result<IReadOnlyList<ServiceListItemResponse>>> ListByCategoryAsync(Guid categoryId)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId);
        if (category is null || !category.IsActive)
        {
            return Error.NotFound("Catalog.CategoryNotFound", "The specified category does not exist.");
        }

        var services = await _serviceRepository.ListActiveByCategoryAsync(categoryId);

        IReadOnlyList<ServiceListItemResponse> response = services.Select(ToListItem).ToList();
        return Result.Success(response);
    }

    public async Task<Result<ServiceDetailResponse>> GetDetailBySlugAsync(string slug)
    {
        var service = await _serviceRepository.GetBySlugAsync(slug);
        if (service is null || !service.IsActive)
        {
            return Error.NotFound("Catalog.ServiceNotFound", "The specified service does not exist.");
        }

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
