using System.Text.Json;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Landing;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin curation of the home page's three configurable sections. Each
/// mutation validates the submitted ids against the live catalog, then
/// replaces that section wholesale - the submitted order becomes
/// <see cref="LandingSelection.SortOrder"/>, so the admin screen never has to
/// manage sort values itself.
/// </summary>
public class LandingManagementService : ILandingManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILandingSelectionRepository _selectionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IAuditLogWriter _auditLogWriter;

    public LandingManagementService(
        ILandingSelectionRepository selectionRepository,
        ICategoryRepository categoryRepository,
        IServiceRepository serviceRepository,
        IAuditLogWriter auditLogWriter)
    {
        _selectionRepository = selectionRepository;
        _categoryRepository = categoryRepository;
        _serviceRepository = serviceRepository;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<LandingConfigResponse> GetConfigAsync()
    {
        var selections = await _selectionRepository.ListAllAsync();

        var categoryIds = selections.Where(s => s.CategoryId is not null).Select(s => s.CategoryId!.Value).Distinct().ToList();
        var serviceIds = selections.Where(s => s.ServiceId is not null).Select(s => s.ServiceId!.Value).Distinct().ToList();

        var categories = (await _categoryRepository.ListByIdsAsync(categoryIds)).ToDictionary(c => c.Id);
        var services = (await _serviceRepository.ListByIdsAsync(serviceIds)).ToDictionary(s => s.Id);

        // Parent names for the "Category → Sub-category" label; fetched in one
        // extra query rather than per row.
        var parentIds = categories.Values
            .Where(c => c.ParentCategoryId is not null)
            .Select(c => c.ParentCategoryId!.Value)
            .Distinct()
            .ToList();
        var parentNames = await _categoryRepository.GetNamesByIdsAsync(parentIds);

        var newAndTrending = selections
            .Where(s => s.SectionType == LandingSectionType.NewAndTrending && s.CategoryId is not null)
            .Select(s => new { Selection = s, Category = categories.GetValueOrDefault(s.CategoryId!.Value) })
            .Where(x => x.Category is not null)
            .Select(x => new LandingNewAndTrendingItemResponse(
                x.Category!.Id,
                x.Category.Name,
                ParentNameOf(x.Category, parentNames),
                x.Selection.SortOrder))
            .ToList();

        var mostBooked = selections
            .Where(s => s.SectionType == LandingSectionType.MostBooked && s.ServiceId is not null)
            .Select(s => ToServiceItem(s, services, categories))
            .OfType<LandingServiceItemResponse>()
            .ToList();

        var categorySections = selections
            .Where(s => s.SectionType == LandingSectionType.CategorySection && s.CategoryId is not null)
            .GroupBy(s => s.CategoryId!.Value)
            .Where(g => categories.ContainsKey(g.Key))
            .Select(g => new LandingCategorySectionItemResponse(
                g.Key,
                categories[g.Key].Name,
                g.OrderBy(s => s.SortOrder)
                    .Select(s => ToServiceItem(s, services, categories))
                    .OfType<LandingServiceItemResponse>()
                    .ToList()))
            .OrderBy(x => x.CategoryName)
            .ToList();

        return new LandingConfigResponse(newAndTrending, mostBooked, categorySections);
    }

    public async Task<Result> UpdateNewAndTrendingAsync(UpdateNewAndTrendingRequest request)
    {
        var ids = Distinct(request.CategoryIds);

        // Only existence + active are enforced. Requiring ParentCategoryId is
        // deliberately NOT a hard rule: the admin picker already presents the
        // tree as Category → Sub-category, and a catalog whose categories are
        // still flat would otherwise be unable to use this section at all.
        var categories = await _categoryRepository.ListByIdsAsync(ids);
        var missing = ids.Except(categories.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
        {
            return Result.Failure(Error.NotFound(
                "Category.NotFound", $"{missing.Count} of the selected categories no longer exist."));
        }

        var replacements = ids
            .Select((categoryId, index) => LandingSelection.ForNewAndTrending(Guid.NewGuid(), categoryId, index))
            .ToList();

        await _selectionRepository.ReplaceSectionAsync(LandingSectionType.NewAndTrending, replacements);
        await WriteAudit("NewAndTrending", ids);

        return Result.Success();
    }

    public async Task<Result> UpdateMostBookedAsync(UpdateMostBookedRequest request)
    {
        var ids = Distinct(request.ServiceIds);

        var services = await _serviceRepository.ListByIdsAsync(ids);
        var missing = ids.Except(services.Select(s => s.Id)).ToList();
        if (missing.Count > 0)
        {
            return Result.Failure(Error.NotFound(
                "Service.NotFound", $"{missing.Count} of the selected services no longer exist."));
        }

        var replacements = ids
            .Select((serviceId, index) => LandingSelection.ForMostBooked(Guid.NewGuid(), serviceId, index))
            .ToList();

        await _selectionRepository.ReplaceSectionAsync(LandingSectionType.MostBooked, replacements);
        await WriteAudit("MostBooked", ids);

        return Result.Success();
    }

    public async Task<Result> UpdateCategorySectionAsync(Guid categoryId, UpdateCategorySectionRequest request)
    {
        var ids = Distinct(request.ServiceIds);

        if (ids.Count > LandingSelection.MaxServicesPerCategorySection)
        {
            return Result.Failure(Error.Validation(
                "LandingSelection.TooManyServices",
                $"A category section shows at most {LandingSelection.MaxServicesPerCategorySection} services."));
        }

        var category = await _categoryRepository.GetByIdAsync(categoryId);
        if (category is null)
        {
            return Result.Failure(Error.NotFound("Category.NotFound", "The specified category does not exist."));
        }

        var services = await _serviceRepository.ListByIdsAsync(ids);
        var missing = ids.Except(services.Select(s => s.Id)).ToList();
        if (missing.Count > 0)
        {
            return Result.Failure(Error.NotFound(
                "Service.NotFound", $"{missing.Count} of the selected services no longer exist."));
        }

        // A strip is headed by its own category, so a service from elsewhere
        // would render under a heading it does not belong to.
        var foreign = services.Where(s => s.CategoryId != categoryId).Select(s => s.Name).ToList();
        if (foreign.Count > 0)
        {
            return Result.Failure(Error.Validation(
                "LandingSelection.ServiceCategoryMismatch",
                $"These services do not belong to this category: {string.Join(", ", foreign)}."));
        }

        var replacements = ids
            .Select((serviceId, index) => LandingSelection.ForCategorySection(Guid.NewGuid(), categoryId, serviceId, index))
            .ToList();

        await _selectionRepository.ReplaceCategorySectionAsync(categoryId, replacements);
        await WriteAudit($"CategorySection:{categoryId}", ids);

        return Result.Success();
    }

    /// <summary>Preserves submitted order (which becomes the display order) while dropping repeats.</summary>
    private static List<Guid> Distinct(IReadOnlyList<Guid>? ids) =>
        ids is null ? [] : ids.Distinct().ToList();

    private static string ParentNameOf(Category category, IReadOnlyDictionary<Guid, string> parentNames) =>
        category.ParentCategoryId is null
            ? string.Empty
            : parentNames.GetValueOrDefault(category.ParentCategoryId.Value, string.Empty);

    private static LandingServiceItemResponse? ToServiceItem(
        LandingSelection selection,
        IReadOnlyDictionary<Guid, Service> services,
        IReadOnlyDictionary<Guid, Category> categories)
    {
        if (selection.ServiceId is null || !services.TryGetValue(selection.ServiceId.Value, out var service))
        {
            return null;
        }

        return new LandingServiceItemResponse(
            service.Id,
            service.Name,
            categories.GetValueOrDefault(service.CategoryId)?.Name ?? string.Empty,
            service.Price,
            selection.SortOrder);
    }

    private Task WriteAudit(string section, IReadOnlyList<Guid> ids) =>
        _auditLogWriter.WriteAsync(new AuditEntry(
            "LandingSelection",
            section,
            "Updated",
            OldValues: null,
            NewValues: JsonSerializer.Serialize(ids, JsonOptions)));
}
