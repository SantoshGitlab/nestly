using Nestly.Application;
using Nestly.Application.Landing;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Assembles the curated home page from the admin's selections. Everything is
/// resolved in a fixed number of queries (selections, then one batched read
/// per entity type) rather than per card, so adding picks never adds round
/// trips.
///
/// Inactive categories/services are dropped at read time: deactivating a
/// service in the catalog should remove it from the home page immediately,
/// without an admin also having to un-pick it here.
/// </summary>
public class LandingQueryService : ILandingQueryService
{
    private readonly ILandingSelectionRepository _selectionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IServiceRepository _serviceRepository;

    public LandingQueryService(
        ILandingSelectionRepository selectionRepository,
        ICategoryRepository categoryRepository,
        IServiceRepository serviceRepository)
    {
        _selectionRepository = selectionRepository;
        _categoryRepository = categoryRepository;
        _serviceRepository = serviceRepository;
    }

    public async Task<HomeLandingResponse> GetHomeAsync()
    {
        var selections = await _selectionRepository.ListAllAsync();
        if (selections.Count == 0)
        {
            return new HomeLandingResponse([], [], []);
        }

        var categoryIds = selections.Where(s => s.CategoryId is not null).Select(s => s.CategoryId!.Value).Distinct().ToList();
        var serviceIds = selections.Where(s => s.ServiceId is not null).Select(s => s.ServiceId!.Value).Distinct().ToList();

        var categories = (await _categoryRepository.ListByIdsAsync(categoryIds))
            .Where(c => c.IsActive)
            .ToDictionary(c => c.Id);
        var services = (await _serviceRepository.ListByIdsAsync(serviceIds))
            .Where(s => s.IsActive)
            .ToDictionary(s => s.Id);

        var parentIds = categories.Values
            .Where(c => c.ParentCategoryId is not null)
            .Select(c => c.ParentCategoryId!.Value)
            .Distinct()
            .ToList();
        var parentNames = await _categoryRepository.GetNamesByIdsAsync(parentIds);

        var newAndTrending = selections
            .Where(s => s.SectionType == LandingSectionType.NewAndTrending && s.CategoryId is not null)
            .OrderBy(s => s.SortOrder)
            .Select(s => categories.GetValueOrDefault(s.CategoryId!.Value))
            .OfType<Category>()
            .Select(c => new LandingSubCategoryResponse(
                c.Id,
                c.Name,
                c.Slug,
                c.BannerUrl,
                c.ParentCategoryId is null
                    ? string.Empty
                    : parentNames.GetValueOrDefault(c.ParentCategoryId.Value, string.Empty)))
            .ToList();

        var mostBooked = selections
            .Where(s => s.SectionType == LandingSectionType.MostBooked && s.ServiceId is not null)
            .OrderBy(s => s.SortOrder)
            .Select(s => services.GetValueOrDefault(s.ServiceId!.Value))
            .OfType<Service>()
            .Select(ToServiceCard)
            .ToList();

        var categorySections = selections
            .Where(s => s.SectionType == LandingSectionType.CategorySection && s.CategoryId is not null && s.ServiceId is not null)
            .GroupBy(s => s.CategoryId!.Value)
            .Where(g => categories.ContainsKey(g.Key))
            .Select(g => new LandingCategorySectionResponse(
                g.Key,
                categories[g.Key].Name,
                categories[g.Key].Slug,
                g.OrderBy(s => s.SortOrder)
                    .Select(s => services.GetValueOrDefault(s.ServiceId!.Value))
                    .OfType<Service>()
                    .Select(ToServiceCard)
                    .ToList()))
            // A heading whose every pick was deactivated would render as an
            // empty strip, so it is dropped entirely.
            .Where(section => section.Services.Count > 0)
            .OrderBy(section => categories[section.CategoryId].SortOrder)
            .ThenBy(section => section.CategoryName)
            .ToList();

        return new HomeLandingResponse(newAndTrending, mostBooked, categorySections);
    }

    private static LandingServiceResponse ToServiceCard(Service service) =>
        new(service.Id, service.Name, service.Slug, service.CoverImageUrl, service.Price);
}
