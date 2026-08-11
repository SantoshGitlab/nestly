using System.Text.Json;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Abstractions.Caching;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Admin CRUD over a category's optional service-group section headers -
/// every mutation explicitly evicts the parent category's catalog cache
/// entry, since <see cref="ServiceGroup"/> raises no domain events of its
/// own (mirrors <see cref="ServiceAddOnGroupManagementService"/>).
/// </summary>
public class ServiceGroupManagementService : IServiceGroupManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceGroupRepository _groupRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICacheService _cache;

    public ServiceGroupManagementService(
        IServiceGroupRepository groupRepository,
        IServiceRepository serviceRepository,
        ICategoryRepository categoryRepository,
        IAuditLogWriter auditLogWriter,
        ICacheService cache)
    {
        _groupRepository = groupRepository;
        _serviceRepository = serviceRepository;
        _categoryRepository = categoryRepository;
        _auditLogWriter = auditLogWriter;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ServiceGroupAdminResponse>> ListAsync(Guid? categoryId)
    {
        var groups = await _groupRepository.ListAllAsync(categoryId);
        var categoryNames = await _categoryRepository.GetNamesByIdsAsync(groups.Select(g => g.CategoryId).Distinct().ToList());
        return groups.Select(g => ToResponse(g, categoryNames)).ToList();
    }

    public async Task<Result<ServiceGroupAdminResponse>> GetByIdAsync(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group is null)
        {
            return NotFound();
        }

        var category = await _categoryRepository.GetByIdAsync(group.CategoryId);
        return ToResponse(group, category?.Name ?? string.Empty);
    }

    public async Task<Result<ServiceGroupAdminResponse>> CreateAsync(ServiceGroupCreateRequest request)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
        if (category is null)
        {
            return Error.NotFound("Category.NotFound", "The specified category does not exist.");
        }

        var group = new ServiceGroup(Guid.NewGuid(), request.CategoryId, request.Name);
        group.SetSortOrder(request.SortOrder);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceGroup", group.Id.ToString(), "Created", OldValues: null, NewValues: Serialize(group, category.Name)));

        await _groupRepository.AddAsync(group);
        await InvalidateCategoryCache(request.CategoryId);

        return ToResponse(group, category.Name);
    }

    public async Task<Result<ServiceGroupAdminResponse>> UpdateAsync(Guid id, ServiceGroupUpdateRequest request)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group is null)
        {
            return NotFound();
        }

        var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
        if (category is null)
        {
            return Error.NotFound("Category.NotFound", "The specified category does not exist.");
        }

        var oldCategory = await _categoryRepository.GetByIdAsync(group.CategoryId);
        string oldValues = Serialize(group, oldCategory?.Name ?? string.Empty);
        Guid oldCategoryId = group.CategoryId;

        group.SetCategoryId(request.CategoryId);
        group.SetName(request.Name);
        group.SetSortOrder(request.SortOrder);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceGroup", group.Id.ToString(), "Updated", oldValues, Serialize(group, category.Name)));

        await _groupRepository.UpdateAsync(group);
        await InvalidateCategoryCache(oldCategoryId);
        if (oldCategoryId != request.CategoryId)
        {
            await InvalidateCategoryCache(request.CategoryId);
        }

        return ToResponse(group, category.Name);
    }

    public async Task<Result> SetActiveAsync(Guid id, bool isActive)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group is null)
        {
            return Result.Failure(NotFound().Error);
        }

        if (isActive) group.Activate(); else group.Deactivate();

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceGroup", group.Id.ToString(), isActive ? "Activated" : "Deactivated"));

        await _groupRepository.UpdateAsync(group);
        await InvalidateCategoryCache(group.CategoryId);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group is null)
        {
            return Result.Failure(NotFound().Error);
        }

        if (await _serviceRepository.ExistsByServiceGroupIdAsync(id))
        {
            return Result.Failure(Error.Conflict(
                "ServiceGroup.InUse", "This group still has services assigned to it. Ungroup or reassign them first."));
        }

        var category = await _categoryRepository.GetByIdAsync(group.CategoryId);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceGroup", group.Id.ToString(), "Deleted", OldValues: Serialize(group, category?.Name ?? string.Empty)));

        await _groupRepository.DeleteAsync(group);
        await InvalidateCategoryCache(group.CategoryId);

        return Result.Success();
    }

    private Task InvalidateCategoryCache(Guid categoryId) => _cache.RemoveAsync(CacheKeys.Category(categoryId));

    private static Result<ServiceGroupAdminResponse> NotFound() =>
        Error.NotFound("ServiceGroup.NotFound", "The specified service group does not exist.");

    private static string Serialize(ServiceGroup group, string categoryName) =>
        JsonSerializer.Serialize(ToResponse(group, categoryName), JsonOptions);

    private static ServiceGroupAdminResponse ToResponse(ServiceGroup group, IReadOnlyDictionary<Guid, string> categoryNames) =>
        ToResponse(group, categoryNames.GetValueOrDefault(group.CategoryId, string.Empty));

    private static ServiceGroupAdminResponse ToResponse(ServiceGroup group, string categoryName) => new(
        group.Id,
        group.CategoryId,
        categoryName,
        group.Name,
        group.IsActive,
        group.SortOrder);
}
