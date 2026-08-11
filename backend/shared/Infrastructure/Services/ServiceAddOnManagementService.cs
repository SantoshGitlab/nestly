using System.Text.Json;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Admin CRUD over add-ons and their mapping to services (SRS 12.7, task 107).</summary>
public class ServiceAddOnManagementService : IServiceAddOnManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceAddOnRepository _addOnRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceAddOnGroupRepository _groupRepository;
    private readonly IAuditLogWriter _auditLogWriter;

    public ServiceAddOnManagementService(
        IServiceAddOnRepository addOnRepository,
        IServiceRepository serviceRepository,
        IServiceAddOnGroupRepository groupRepository,
        IAuditLogWriter auditLogWriter)
    {
        _addOnRepository = addOnRepository;
        _serviceRepository = serviceRepository;
        _groupRepository = groupRepository;
        _auditLogWriter = auditLogWriter;
    }

    public async Task<IReadOnlyList<ServiceAddOnAdminResponse>> ListAsync(Guid? serviceId)
    {
        var addOns = await _addOnRepository.ListAllAsync(serviceId);
        var serviceNames = await BuildServiceNameLookupAsync(addOns.Select(a => a.ServiceId));
        return addOns.Select(a => ToResponse(a, serviceNames)).ToList();
    }

    public async Task<Result<ServiceAddOnAdminResponse>> GetByIdAsync(Guid id)
    {
        var addOn = await _addOnRepository.GetByIdAsync(id);
        if (addOn is null)
        {
            return NotFound();
        }

        var service = await _serviceRepository.GetByIdAsync(addOn.ServiceId);
        return ToResponse(addOn, service?.Name ?? string.Empty);
    }

    public async Task<Result<ServiceAddOnAdminResponse>> CreateAsync(ServiceAddOnCreateRequest request)
    {
        var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
        if (service is null)
        {
            return Error.NotFound("Service.NotFound", "The specified service does not exist.");
        }

        var groupValidation = await ValidateGroupAsync(request.GroupId, request.ServiceId);
        if (groupValidation.IsFailure)
        {
            return Result.Failure<ServiceAddOnAdminResponse>(groupValidation.Error);
        }

        var addOn = new ServiceAddOn(Guid.NewGuid(), request.ServiceId, request.Name, request.Price);
        addOn.SetDescription(request.Description);
        addOn.SetSortOrder(request.SortOrder);
        addOn.SetQuantityAllowed(request.IsQuantityAllowed);
        addOn.SetMandatory(request.IsMandatory);
        addOn.SetGroupId(request.GroupId);

        // Staged before AddAsync so its own SaveChangesAsync commits the
        // audit row in the same transaction as the new add-on.
        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceAddOn", addOn.Id.ToString(), "Created", OldValues: null, NewValues: Serialize(addOn, service.Name)));

        await _addOnRepository.AddAsync(addOn);

        return ToResponse(addOn, service.Name);
    }

    public async Task<Result<ServiceAddOnAdminResponse>> UpdateAsync(Guid id, ServiceAddOnUpdateRequest request)
    {
        var addOn = await _addOnRepository.GetByIdAsync(id);
        if (addOn is null)
        {
            return NotFound();
        }

        var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
        if (service is null)
        {
            return Error.NotFound("Service.NotFound", "The specified service does not exist.");
        }

        var groupValidation = await ValidateGroupAsync(request.GroupId, request.ServiceId);
        if (groupValidation.IsFailure)
        {
            return Result.Failure<ServiceAddOnAdminResponse>(groupValidation.Error);
        }

        var oldService = await _serviceRepository.GetByIdAsync(addOn.ServiceId);
        string oldValues = Serialize(addOn, oldService?.Name ?? string.Empty);

        addOn.SetServiceId(request.ServiceId);
        addOn.SetName(request.Name);
        addOn.SetDescription(request.Description);
        addOn.SetPrice(request.Price);
        addOn.SetSortOrder(request.SortOrder);
        addOn.SetQuantityAllowed(request.IsQuantityAllowed);
        addOn.SetMandatory(request.IsMandatory);
        addOn.SetGroupId(request.GroupId);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceAddOn", addOn.Id.ToString(), "Updated", oldValues, Serialize(addOn, service.Name)));

        await _addOnRepository.UpdateAsync(addOn);
        return ToResponse(addOn, service.Name);
    }

    public async Task<Result> SetActiveAsync(Guid id, bool isActive)
    {
        var addOn = await _addOnRepository.GetByIdAsync(id);
        if (addOn is null)
        {
            return Result.Failure(NotFound().Error);
        }

        if (isActive) addOn.Activate(); else addOn.Deactivate();

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceAddOn", addOn.Id.ToString(), isActive ? "Activated" : "Deactivated"));

        await _addOnRepository.UpdateAsync(addOn);
        return Result.Success();
    }

    // Task NESTLY-011: one batched lookup instead of a GetByIdAsync per
    // distinct service id (mirrors ReferralAdminService.SearchAsync's use of
    // the equivalent ICustomerRepository.GetNamesByIdsAsync).
    private async Task<IReadOnlyDictionary<Guid, string>> BuildServiceNameLookupAsync(IEnumerable<Guid> serviceIds) =>
        await _serviceRepository.GetNamesByIdsAsync(serviceIds.Distinct().ToList());

    private static Result<ServiceAddOnAdminResponse> NotFound() =>
        Error.NotFound("ServiceAddOn.NotFound", "The specified add-on does not exist.");

    /// <summary>Phase 3 catalog redesign: when a group is supplied, it must exist and belong to the same service the add-on is being mapped to.</summary>
    private async Task<Result> ValidateGroupAsync(Guid? groupId, Guid serviceId)
    {
        if (groupId is null)
        {
            return Result.Success();
        }

        var group = await _groupRepository.GetByIdAsync(groupId.Value);
        if (group is null)
        {
            return Result.Failure(Error.NotFound("ServiceAddOnGroup.NotFound", "The specified add-on group does not exist."));
        }

        if (group.ServiceId != serviceId)
        {
            return Result.Failure(Error.Validation(
                "ServiceAddOnGroup.ServiceMismatch", "The specified add-on group belongs to a different service."));
        }

        return Result.Success();
    }

    private static string Serialize(ServiceAddOn addOn, string serviceName) =>
        JsonSerializer.Serialize(ToResponse(addOn, serviceName), JsonOptions);

    private static ServiceAddOnAdminResponse ToResponse(ServiceAddOn addOn, IReadOnlyDictionary<Guid, string> serviceNames) =>
        ToResponse(addOn, serviceNames.GetValueOrDefault(addOn.ServiceId, string.Empty));

    private static ServiceAddOnAdminResponse ToResponse(ServiceAddOn addOn, string serviceName) => new(
        addOn.Id,
        addOn.ServiceId,
        serviceName,
        addOn.Name,
        addOn.Description,
        addOn.Price,
        addOn.IsActive,
        addOn.SortOrder,
        addOn.IsQuantityAllowed,
        addOn.IsMandatory,
        addOn.GroupId);
}
