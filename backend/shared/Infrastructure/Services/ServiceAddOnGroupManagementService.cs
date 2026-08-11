using System.Text.Json;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Abstractions.Caching;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Admin CRUD over add-on groups and their selection rules (Phase 3 catalog redesign).</summary>
public class ServiceAddOnGroupManagementService : IServiceAddOnGroupManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceAddOnGroupRepository _groupRepository;
    private readonly IServiceAddOnRepository _addOnRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICacheService _cache;

    public ServiceAddOnGroupManagementService(
        IServiceAddOnGroupRepository groupRepository,
        IServiceAddOnRepository addOnRepository,
        IServiceRepository serviceRepository,
        IAuditLogWriter auditLogWriter,
        ICacheService cache)
    {
        _groupRepository = groupRepository;
        _addOnRepository = addOnRepository;
        _serviceRepository = serviceRepository;
        _auditLogWriter = auditLogWriter;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ServiceAddOnGroupAdminResponse>> ListAsync(Guid? serviceId)
    {
        var groups = await _groupRepository.ListAllAsync(serviceId);
        var serviceNames = await _serviceRepository.GetNamesByIdsAsync(groups.Select(g => g.ServiceId).Distinct().ToList());
        return groups.Select(g => ToResponse(g, serviceNames)).ToList();
    }

    public async Task<Result<ServiceAddOnGroupAdminResponse>> GetByIdAsync(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group is null)
        {
            return NotFound();
        }

        var service = await _serviceRepository.GetByIdAsync(group.ServiceId);
        return ToResponse(group, service?.Name ?? string.Empty);
    }

    public async Task<Result<ServiceAddOnGroupAdminResponse>> CreateAsync(ServiceAddOnGroupCreateRequest request)
    {
        var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
        if (service is null)
        {
            return Error.NotFound("Service.NotFound", "The specified service does not exist.");
        }

        var group = new ServiceAddOnGroup(
            Guid.NewGuid(), request.ServiceId, request.Name, Enum.Parse<AddOnGroupSelectionType>(request.SelectionType));
        var ruleResult = ApplySelectionRule(group, request.MinSelect, request.MaxSelect);
        if (ruleResult.IsFailure)
        {
            return Result.Failure<ServiceAddOnGroupAdminResponse>(ruleResult.Error);
        }
        group.SetSortOrder(request.SortOrder);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceAddOnGroup", group.Id.ToString(), "Created", OldValues: null, NewValues: Serialize(group, service.Name)));

        await _groupRepository.AddAsync(group);
        await InvalidateServiceCache(request.ServiceId);

        return ToResponse(group, service.Name);
    }

    public async Task<Result<ServiceAddOnGroupAdminResponse>> UpdateAsync(Guid id, ServiceAddOnGroupUpdateRequest request)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group is null)
        {
            return NotFound();
        }

        var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
        if (service is null)
        {
            return Error.NotFound("Service.NotFound", "The specified service does not exist.");
        }

        var oldService = await _serviceRepository.GetByIdAsync(group.ServiceId);
        string oldValues = Serialize(group, oldService?.Name ?? string.Empty);
        Guid oldServiceId = group.ServiceId;

        group.SetServiceId(request.ServiceId);
        group.SetName(request.Name);
        group.SetSelectionType(Enum.Parse<AddOnGroupSelectionType>(request.SelectionType));
        var ruleResult = ApplySelectionRule(group, request.MinSelect, request.MaxSelect);
        if (ruleResult.IsFailure)
        {
            return Result.Failure<ServiceAddOnGroupAdminResponse>(ruleResult.Error);
        }
        group.SetSortOrder(request.SortOrder);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceAddOnGroup", group.Id.ToString(), "Updated", oldValues, Serialize(group, service.Name)));

        await _groupRepository.UpdateAsync(group);
        await InvalidateServiceCache(oldServiceId);
        if (oldServiceId != request.ServiceId)
        {
            await InvalidateServiceCache(request.ServiceId);
        }

        return ToResponse(group, service.Name);
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group is null)
        {
            return Result.Failure(NotFound().Error);
        }

        if (await _addOnRepository.ExistsByGroupIdAsync(id))
        {
            return Result.Failure(Error.Conflict(
                "ServiceAddOnGroup.InUse", "This group still has add-ons assigned to it. Ungroup or reassign them first."));
        }

        var service = await _serviceRepository.GetByIdAsync(group.ServiceId);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceAddOnGroup", group.Id.ToString(), "Deleted", OldValues: Serialize(group, service?.Name ?? string.Empty)));

        await _groupRepository.DeleteAsync(group);
        await InvalidateServiceCache(group.ServiceId);

        return Result.Success();
    }

    private static Result ApplySelectionRule(ServiceAddOnGroup group, int minSelect, int? maxSelect)
    {
        try
        {
            group.SetSelectionRule(minSelect, maxSelect);
            return Result.Success();
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(Error.Validation("ServiceAddOnGroup.InvalidSelectionRule", ex.Message));
        }
    }

    private Task InvalidateServiceCache(Guid serviceId) => _cache.RemoveAsync(CacheKeys.Service(serviceId));

    private static Result<ServiceAddOnGroupAdminResponse> NotFound() =>
        Error.NotFound("ServiceAddOnGroup.NotFound", "The specified add-on group does not exist.");

    private static string Serialize(ServiceAddOnGroup group, string serviceName) =>
        JsonSerializer.Serialize(ToResponse(group, serviceName), JsonOptions);

    private static ServiceAddOnGroupAdminResponse ToResponse(ServiceAddOnGroup group, IReadOnlyDictionary<Guid, string> serviceNames) =>
        ToResponse(group, serviceNames.GetValueOrDefault(group.ServiceId, string.Empty));

    private static ServiceAddOnGroupAdminResponse ToResponse(ServiceAddOnGroup group, string serviceName) => new(
        group.Id,
        group.ServiceId,
        serviceName,
        group.Name,
        group.SelectionType.ToString(),
        group.MinSelect,
        group.MaxSelect,
        group.SortOrder);
}
