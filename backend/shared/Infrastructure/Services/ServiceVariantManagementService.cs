using System.Text.Json;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Abstractions.Caching;
using Nestly.Application.Catalog;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Admin CRUD over a service's variants (Phase 3 catalog redesign).</summary>
public class ServiceVariantManagementService : IServiceVariantManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceVariantRepository _variantRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICacheService _cache;

    public ServiceVariantManagementService(
        IServiceVariantRepository variantRepository,
        IServiceRepository serviceRepository,
        IAuditLogWriter auditLogWriter,
        ICacheService cache)
    {
        _variantRepository = variantRepository;
        _serviceRepository = serviceRepository;
        _auditLogWriter = auditLogWriter;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ServiceVariantAdminResponse>> ListAsync(Guid serviceId)
    {
        var variants = await _variantRepository.ListByServiceAsync(serviceId);
        return variants.Select(ToResponse).ToList();
    }

    public async Task<Result<ServiceVariantAdminResponse>> GetByIdAsync(Guid id)
    {
        var variant = await _variantRepository.GetByIdAsync(id);
        return variant is null ? NotFound() : ToResponse(variant);
    }

    public async Task<Result<ServiceVariantAdminResponse>> CreateAsync(Guid serviceId, ServiceVariantCreateRequest request)
    {
        var service = await _serviceRepository.GetByIdAsync(serviceId);
        if (service is null)
        {
            return Error.NotFound("Service.NotFound", "The specified service does not exist.");
        }

        var variant = new ServiceVariant(Guid.NewGuid(), serviceId, request.Name, request.Price, request.DurationMinutes);
        variant.SetInclusionsOverride(request.InclusionsOverride);
        variant.SetSortOrder(request.SortOrder);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceVariant", variant.Id.ToString(), "Created", OldValues: null, NewValues: Serialize(variant)));

        await _variantRepository.AddAsync(variant);
        await InvalidateServiceCache(serviceId);

        return ToResponse(variant);
    }

    public async Task<Result<ServiceVariantAdminResponse>> UpdateAsync(Guid id, ServiceVariantUpdateRequest request)
    {
        var variant = await _variantRepository.GetByIdAsync(id);
        if (variant is null)
        {
            return NotFound();
        }

        string oldValues = Serialize(variant);

        variant.SetName(request.Name);
        variant.SetPrice(request.Price);
        variant.SetDuration(request.DurationMinutes);
        variant.SetInclusionsOverride(request.InclusionsOverride);
        variant.SetSortOrder(request.SortOrder);

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceVariant", variant.Id.ToString(), "Updated", oldValues, Serialize(variant)));

        await _variantRepository.UpdateAsync(variant);
        await InvalidateServiceCache(variant.ServiceId);

        return ToResponse(variant);
    }

    public async Task<Result> SetActiveAsync(Guid id, bool isActive)
    {
        var variant = await _variantRepository.GetByIdAsync(id);
        if (variant is null)
        {
            return Result.Failure(NotFound().Error);
        }

        if (isActive) variant.Activate(); else variant.Deactivate();

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceVariant", variant.Id.ToString(), isActive ? "Activated" : "Deactivated"));

        await _variantRepository.UpdateAsync(variant);
        await InvalidateServiceCache(variant.ServiceId);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        var variant = await _variantRepository.GetByIdAsync(id);
        if (variant is null)
        {
            return Result.Failure(NotFound().Error);
        }

        await _auditLogWriter.WriteAsync(new AuditEntry(
            "ServiceVariant", variant.Id.ToString(), "Deleted", OldValues: Serialize(variant)));

        await _variantRepository.DeleteAsync(variant);
        await InvalidateServiceCache(variant.ServiceId);

        return Result.Success();
    }

    private Task InvalidateServiceCache(Guid serviceId) => _cache.RemoveAsync(CacheKeys.Service(serviceId));

    private static Result<ServiceVariantAdminResponse> NotFound() =>
        Error.NotFound("ServiceVariant.NotFound", "The specified variant does not exist.");

    private static string Serialize(ServiceVariant variant) => JsonSerializer.Serialize(ToResponse(variant), JsonOptions);

    private static ServiceVariantAdminResponse ToResponse(ServiceVariant variant) => new(
        variant.Id,
        variant.ServiceId,
        variant.Name,
        variant.Price,
        variant.DurationMinutes,
        variant.InclusionsOverride,
        variant.IsActive,
        variant.SortOrder);
}
