using Nestly.Application;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Combined category/service search (task 42c, SRS 24.3).</summary>
public class CatalogSearchService : ICatalogSearchService
{
    // Task 136c: this endpoint had no pagination or result cap at all - a
    // common term (or an empty-ish query once trimmed) could return every
    // active category/service in the catalog on every request. A common
    // free-text search only ever needs the top handful of matches for
    // display, so this caps rather than adds full page/pageSize plumbing -
    // there is no legitimate use case here (unlike the admin "list all
    // active" lookups reusing SearchActiveAsync with an empty query) that
    // needs the complete result set.
    private const int MaxResultsPerType = 20;

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

        var categories = await _categoryRepository.SearchActiveAsync(trimmed, MaxResultsPerType);
        var services = await _serviceRepository.SearchActiveAsync(trimmed, MaxResultsPerType);

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
