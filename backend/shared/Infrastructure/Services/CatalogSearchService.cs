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

        // Own-active flag alone isn't enough once categories nest (SRS
        // 11.5.4, 12.5.3): a sub-category or service can stay individually
        // active while a deactivated ancestor should still hide it from
        // customer-facing discovery, matching the same rule applied to
        // direct category/service detail lookups.
        var visibleCategories = new List<Category>(categories.Count);
        foreach (var category in categories)
        {
            if (await IsVisibleInHierarchyAsync(category))
            {
                visibleCategories.Add(category);
            }
        }

        var visibleServices = new List<Service>(services.Count);
        foreach (var service in services)
        {
            var owningCategory = await _categoryRepository.GetByIdAsync(service.CategoryId);
            if (owningCategory is not null && await IsVisibleInHierarchyAsync(owningCategory))
            {
                visibleServices.Add(service);
            }
        }

        IReadOnlyList<CategorySummaryResponse> categoryResults = visibleCategories.Select(ToSummary).ToList();
        IReadOnlyList<ServiceListItemResponse> serviceResults = visibleServices.Select(ToListItem).ToList();

        return new CatalogSearchResponse(categoryResults, serviceResults);
    }

    /// <summary>
    /// Whether <paramref name="category"/> and every one of its ancestor
    /// categories are active - mirrors
    /// <c>CategoryQueryService.IsVisibleInHierarchyAsync</c> /
    /// <c>ServiceQueryService.IsCategoryVisibleInHierarchyAsync</c>.
    /// Bounded to 32 hops as a defensive ceiling against malformed/cyclical
    /// parent data (a true cycle is already rejected at write time by
    /// <c>CategoryManagementService</c>).
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

    private static CategorySummaryResponse ToSummary(Category category) => new(
        category.Id,
        category.Name,
        category.Slug,
        category.IconUrl,
        category.BannerUrl,
        category.PageBannerUrl,
        category.IsFeatured);

    private static ServiceListItemResponse ToListItem(Service service) => new(
        service.Id,
        service.Name,
        service.Slug,
        service.Description,
        service.Price,
        service.CoverImageUrl,
        service.DurationMinutes);
}
