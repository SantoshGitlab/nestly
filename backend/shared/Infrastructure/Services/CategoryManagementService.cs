using System.Text.Json;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Admin CRUD over categories (SRS 12.5, tasks 103a-103e).</summary>
public class CategoryManagementService : ICategoryManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICategoryRepository _categoryRepository;
    private readonly IAuditLogWriter _auditLogWriter;

    public CategoryManagementService(ICategoryRepository categoryRepository, IAuditLogWriter auditLogWriter)
    {
        _categoryRepository = categoryRepository;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<IReadOnlyList<CategoryResponse>> ListAsync()
    {
        var categories = await _categoryRepository.ListAllAsync();
        return categories.Select(ToResponse).ToList();
    }

    public async Task<Result<CategoryResponse>> GetByIdAsync(Guid id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        return category is null
            ? NotFound()
            : ToResponse(category);
    }

    public async Task<Result<CategoryResponse>> CreateAsync(CategoryCreateRequest request)
    {
        if (await _categoryRepository.ExistsBySlugAsync(request.Slug))
        {
            return Error.Conflict("Category.DuplicateSlug", "A category with this slug already exists.");
        }

        var category = new Category(Guid.NewGuid(), request.Name, request.Slug, request.Description);
        category.SetIconUrl(request.IconUrl);
        category.SetBannerUrl(request.BannerUrl);
        category.SetSortOrder(request.SortOrder);
        category.SetSeo(request.SeoTitle, request.SeoMetaDescription);

        // Staged before AddAsync so its own SaveChangesAsync commits the
        // audit row in the same transaction as the new category (IAuditLogWriter's
        // documented contract) - staging it after would leave it flushed only
        // by some later, unrelated save.
        await _auditLogWriter.WriteAsync(new AuditEntry(
            "Category", category.Id.ToString(), "Created", OldValues: null, NewValues: Serialize(category)));

        await _categoryRepository.AddAsync(category);

        return ToResponse(category);
    }

    public async Task<Result<CategoryResponse>> UpdateAsync(Guid id, CategoryUpdateRequest request)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        if (await _categoryRepository.ExistsBySlugAsync(request.Slug, excludeId: id))
        {
            return Error.Conflict("Category.DuplicateSlug", "A category with this slug already exists.");
        }

        string oldValues = Serialize(category);

        category.SetName(request.Name);
        category.SetSlug(request.Slug);
        category.SetDescription(request.Description);
        category.SetIconUrl(request.IconUrl);
        category.SetBannerUrl(request.BannerUrl);
        category.SetSortOrder(request.SortOrder);
        category.SetSeo(request.SeoTitle, request.SeoMetaDescription);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "Category", category.Id.ToString(), "Updated", oldValues, Serialize(category)));

        await _categoryRepository.UpdateAsync(category);
        return ToResponse(category);
    }

    public async Task<Result> SetActiveAsync(Guid id, bool isActive)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category is null)
        {
            return Result.Failure(NotFound());
        }

        if (isActive) category.Activate(); else category.Deactivate();

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "Category", category.Id.ToString(), isActive ? "Activated" : "Deactivated"));

        await _categoryRepository.UpdateAsync(category);
        return Result.Success();
    }

    public async Task<Result> SetFeaturedAsync(Guid id, bool isFeatured)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        if (category is null)
        {
            return Result.Failure(NotFound());
        }

        if (isFeatured) category.Feature(); else category.Unfeature();

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "Category", category.Id.ToString(), isFeatured ? "Featured" : "Unfeatured"));

        await _categoryRepository.UpdateAsync(category);
        return Result.Success();
    }

    private static Error NotFound() =>
        Error.NotFound("Category.NotFound", "The specified category does not exist.");

    private static string Serialize(Category category) => JsonSerializer.Serialize(ToResponse(category), JsonOptions);

    private static CategoryResponse ToResponse(Category category) => new(
        category.Id,
        category.Name,
        category.Slug,
        category.Description,
        category.IconUrl,
        category.BannerUrl,
        category.IsActive,
        category.IsFeatured,
        category.SortOrder,
        category.SeoTitle,
        category.SeoMetaDescription);
}
