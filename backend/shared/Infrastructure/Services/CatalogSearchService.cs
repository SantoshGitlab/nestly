using Nestly.Application;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Combined category/service search (task 42c, SRS 24.3).</summary>
public class CatalogSearchService : ICatalogSearchService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IServiceRepository _serviceRepository;

    public CatalogSearchService(ICategoryRepository categoryRepository, IServiceRepository serviceRepository)
    {
        _categoryRepository = categoryRepository;
        _serviceRepository = serviceRepository;
    }

    public async Task<Result<CatalogSearchResponse>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return Error.Validation("Catalog.SearchQueryTooShort", "Search query must be at least 2 characters.");
        }

        string trimmed = query.Trim();

        var categories = await _categoryRepository.SearchActiveAsync(trimmed);
        var services = await _serviceRepository.SearchActiveAsync(trimmed);

        IReadOnlyList<CategorySummaryResponse> categoryResults = categories.Select(ToSummary).ToList();
        IReadOnlyList<ServiceListItemResponse> serviceResults = services.Select(ToListItem).ToList();

        return new CatalogSearchResponse(categoryResults, serviceResults);
    }

    private static CategorySummaryResponse ToSummary(Category category) => new(
        category.Id,
        category.Name,
        category.Slug,
        category.IconUrl,
        category.BannerUrl,
        category.IsFeatured);

    private static ServiceListItemResponse ToListItem(Service service) => new(
        service.Id,
        service.Name,
        service.Slug,
        service.Description,
        service.Price);
}
