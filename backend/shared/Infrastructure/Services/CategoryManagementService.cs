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
    private readonly ICategoryGroupRepository _categoryGroupRepository;
    private readonly IAuditLogWriter _auditLogWriter;

    public CategoryManagementService(
        ICategoryRepository categoryRepository, ICategoryGroupRepository categoryGroupRepository, IAuditLogWriter auditLogWriter)
    {
        _categoryRepository = categoryRepository;
        _categoryGroupRepository = categoryGroupRepository;
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
        category.SetPageBannerUrl(request.PageBannerUrl);
        category.SetSortOrder(request.SortOrder);
        category.SetSeo(request.SeoTitle, request.SeoMetaDescription);

        var parentValidation = await ValidateParentAsync(category.Id, request.ParentCategoryId);
        if (parentValidation.IsFailure)
        {
            return Result.Failure<CategoryResponse>(parentValidation.Error);
        }
        category.SetParent(request.ParentCategoryId);

        var groupValidation = await ValidateCategoryGroupAsync(request.CategoryGroupId, request.ParentCategoryId);
        if (groupValidation.IsFailure)
        {
            return Result.Failure<CategoryResponse>(groupValidation.Error);
        }
        category.SetCategoryGroupId(request.CategoryGroupId);

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

        var parentValidation = await ValidateParentAsync(category.Id, request.ParentCategoryId);
        if (parentValidation.IsFailure)
        {
            return Result.Failure<CategoryResponse>(parentValidation.Error);
        }

        category.SetName(request.Name);
        category.SetSlug(request.Slug);
        category.SetDescription(request.Description);
        category.SetIconUrl(request.IconUrl);
        category.SetBannerUrl(request.BannerUrl);
        category.SetPageBannerUrl(request.PageBannerUrl);
        category.SetSortOrder(request.SortOrder);
        category.SetSeo(request.SeoTitle, request.SeoMetaDescription);

        try
        {
            category.SetParent(request.ParentCategoryId);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<CategoryResponse>(Error.Validation("Category.SelfParent", ex.Message));
        }

        var groupValidation = await ValidateCategoryGroupAsync(request.CategoryGroupId, request.ParentCategoryId);
        if (groupValidation.IsFailure)
        {
            return Result.Failure<CategoryResponse>(groupValidation.Error);
        }
        category.SetCategoryGroupId(request.CategoryGroupId);
        category.MarkUpdated();

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

    public async Task<IReadOnlyList<CategoryResponse>> ListChildrenAsync(Guid parentCategoryId)
    {
        var children = await _categoryRepository.ListChildrenAsync(parentCategoryId);
        return children.Select(ToResponse).ToList();
    }

    private static Error NotFound() =>
        Error.NotFound("Category.NotFound", "The specified category does not exist.");

    /// <summary>
    /// Phase 3 catalog redesign: the proposed parent must exist and must not
    /// be <paramref name="categoryId"/> itself or a descendant of it (which
    /// would create a cycle in the tree). Bounded to a depth of 10 - there is
    /// no legitimate catalog hierarchy anywhere near that deep, so a chain
    /// that long is itself evidence of a data problem rather than a
    /// tree worth continuing to walk.
    /// </summary>
    private async Task<Result> ValidateParentAsync(Guid categoryId, Guid? proposedParentId)
    {
        if (proposedParentId is null)
        {
            return Result.Success();
        }

        if (!await _categoryRepository.ExistsAsync(proposedParentId.Value))
        {
            return Result.Failure(Error.NotFound("Category.ParentNotFound", "The specified parent category does not exist."));
        }

        Guid? current = proposedParentId;
        for (int depth = 0; current is not null && depth < 10; depth++)
        {
            if (current == categoryId)
            {
                return Result.Failure(Error.Validation(
                    "Category.CircularParent", "This parent assignment would create a circular category tree."));
            }

            var parent = await _categoryRepository.GetByIdAsync(current.Value);
            current = parent?.ParentCategoryId;
        }

        return Result.Success();
    }

    /// <summary>
    /// A category group only makes sense scoped to how one parent organizes
    /// its own children - so the proposed group (if any) must exist and must
    /// belong to the same <paramref name="parentCategoryId"/> this category
    /// is being filed under. A top-level category (null parent) can never
    /// carry a group, since there is no parent whose subcategory listing it
    /// could be a section of.
    /// </summary>
    private async Task<Result> ValidateCategoryGroupAsync(Guid? categoryGroupId, Guid? parentCategoryId)
    {
        if (categoryGroupId is null)
        {
            return Result.Success();
        }

        if (parentCategoryId is null)
        {
            return Result.Failure(Error.Validation(
                "Category.GroupRequiresParent", "A category group can only be assigned to a subcategory (one with a parent)."));
        }

        var group = await _categoryGroupRepository.GetByIdAsync(categoryGroupId.Value);
        if (group is null)
        {
            return Result.Failure(Error.NotFound("CategoryGroup.NotFound", "The specified category group does not exist."));
        }

        if (group.CategoryId != parentCategoryId.Value)
        {
            return Result.Failure(Error.Validation(
                "Category.GroupParentMismatch", "This category group belongs to a different parent category."));
        }

        return Result.Success();
    }

    private static string Serialize(Category category) => JsonSerializer.Serialize(ToResponse(category), JsonOptions);

    private static CategoryResponse ToResponse(Category category) => new(
        category.Id,
        category.Name,
        category.Slug,
        category.Description,
        category.IconUrl,
        category.BannerUrl,
        category.PageBannerUrl,
        category.IsActive,
        category.IsFeatured,
        category.SortOrder,
        category.SeoTitle,
        category.SeoMetaDescription,
        category.ParentCategoryId,
        category.CategoryGroupId);
}
