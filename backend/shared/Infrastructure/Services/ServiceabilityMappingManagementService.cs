using System.Text.Json;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Serviceability;
using Nestly.Application.Settings;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>Admin CRUD over category/city and service/pincode serviceability mappings (SRS 12.9.2, task 111).</summary>
public class ServiceabilityMappingManagementService : IServiceabilityMappingManagementService
{
    private readonly ICategoryCityMappingRepository _categoryCityMappingRepository;
    private readonly IServicePincodeMappingRepository _servicePincodeMappingRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICityRepository _cityRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IPincodeRepository _pincodeRepository;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ISystemSettingsService _systemSettingsService;

    public ServiceabilityMappingManagementService(
        ICategoryCityMappingRepository categoryCityMappingRepository,
        IServicePincodeMappingRepository servicePincodeMappingRepository,
        ICategoryRepository categoryRepository,
        ICityRepository cityRepository,
        IServiceRepository serviceRepository,
        IPincodeRepository pincodeRepository,
        IAuditLogWriter auditLogWriter,
        ISystemSettingsService systemSettingsService)
    {
        _categoryCityMappingRepository = categoryCityMappingRepository;
        _servicePincodeMappingRepository = servicePincodeMappingRepository;
        _categoryRepository = categoryRepository;
        _cityRepository = cityRepository;
        _serviceRepository = serviceRepository;
        _pincodeRepository = pincodeRepository;
        _auditLogWriter = auditLogWriter;
        _systemSettingsService = systemSettingsService;
    }

    public async Task<IReadOnlyList<CategoryLookupResponse>> ListCategoriesAsync()
    {
        // Empty query matches every active category - SearchActiveAsync's
        // Contains("") is always true - reused here rather than adding a
        // parallel "list all" repository method for what is otherwise the
        // same query.
        var categories = await _categoryRepository.SearchActiveAsync(string.Empty);
        return categories.Select(c => new CategoryLookupResponse(c.Id, c.Name)).ToList();
    }

    public async Task<IReadOnlyList<ServiceLookupResponse>> ListServicesAsync()
    {
        var services = await _serviceRepository.SearchActiveAsync(string.Empty);
        return services.Select(s => new ServiceLookupResponse(s.Id, s.Name)).ToList();
    }

    public Task<IReadOnlyList<CategoryCityMappingResponse>> ListCategoryCityMappingsAsync(Guid? categoryId, Guid? cityId) =>
        _categoryCityMappingRepository.ListAsync(categoryId, cityId);

    public async Task<Result<CategoryCityMappingResponse>> CreateCategoryCityMappingAsync(CategoryCityMappingCreateRequest request)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
        if (category is null)
        {
            return Error.NotFound("Serviceability.CategoryNotFound", "The specified category does not exist.");
        }

        var city = await _cityRepository.GetByIdAsync(request.CityId);
        if (city is null)
        {
            return Error.NotFound("Serviceability.CityNotFound", "The specified city does not exist.");
        }

        var existing = await _categoryCityMappingRepository.FindAsync(request.CategoryId, request.CityId);
        if (existing is not null)
        {
            // A mapping already exists - re-enable it if it was suspended
            // rather than failing on the unique-index conflict (see the
            // interface doc comment for why this is idempotent).
            if (!existing.IsActive)
            {
                existing.Activate();
                await _categoryCityMappingRepository.UpdateAsync(existing);
            }

            return new CategoryCityMappingResponse(existing.Id, category.Id, category.Name, city.Id, city.Name, existing.IsActive);
        }

        var mapping = new CategoryCityMapping(Guid.NewGuid(), request.CategoryId, request.CityId);
        await _categoryCityMappingRepository.AddAsync(mapping);
        return new CategoryCityMappingResponse(mapping.Id, category.Id, category.Name, city.Id, city.Name, mapping.IsActive);
    }

    public async Task<Result> ActivateCategoryCityMappingAsync(Guid id)
    {
        var mapping = await _categoryCityMappingRepository.GetByIdAsync(id);
        if (mapping is null)
        {
            return Result.Failure(Error.NotFound("Serviceability.MappingNotFound", "The specified mapping does not exist."));
        }

        mapping.Activate();
        await _categoryCityMappingRepository.UpdateAsync(mapping);
        return Result.Success();
    }

    public async Task<Result> DeactivateCategoryCityMappingAsync(Guid id)
    {
        var mapping = await _categoryCityMappingRepository.GetByIdAsync(id);
        if (mapping is null)
        {
            return Result.Failure(Error.NotFound("Serviceability.MappingNotFound", "The specified mapping does not exist."));
        }

        mapping.Deactivate();
        await _categoryCityMappingRepository.UpdateAsync(mapping);
        return Result.Success();
    }

    public Task<IReadOnlyList<ServicePincodeMappingResponse>> ListServicePincodeMappingsAsync(Guid? serviceId, Guid? pincodeId) =>
        _servicePincodeMappingRepository.ListAsync(serviceId, pincodeId);

    public async Task<Result<ServicePincodeMappingResponse>> CreateServicePincodeMappingAsync(ServicePincodeMappingCreateRequest request)
    {
        var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
        if (service is null)
        {
            return Error.NotFound("Serviceability.ServiceNotFound", "The specified service does not exist.");
        }

        var pincode = await _pincodeRepository.GetByIdAsync(request.PincodeId);
        if (pincode is null)
        {
            return Error.NotFound("Serviceability.PincodeNotFound", "The specified pincode does not exist.");
        }

        var existing = await _servicePincodeMappingRepository.FindAsync(request.ServiceId, request.PincodeId);
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.Activate();
                await _servicePincodeMappingRepository.UpdateAsync(existing);
            }

            return new ServicePincodeMappingResponse(existing.Id, service.Id, service.Name, pincode.Id, pincode.Code, existing.IsActive, existing.IsPinned);
        }

        var mapping = new ServicePincodeMapping(Guid.NewGuid(), request.ServiceId, request.PincodeId);
        await _servicePincodeMappingRepository.AddAsync(mapping);
        return new ServicePincodeMappingResponse(mapping.Id, service.Id, service.Name, pincode.Id, pincode.Code, mapping.IsActive, mapping.IsPinned);
    }

    public async Task<Result> ActivateServicePincodeMappingAsync(Guid id)
    {
        var mapping = await _servicePincodeMappingRepository.GetByIdAsync(id);
        if (mapping is null)
        {
            return Result.Failure(Error.NotFound("Serviceability.MappingNotFound", "The specified mapping does not exist."));
        }

        mapping.Activate();
        await _servicePincodeMappingRepository.UpdateAsync(mapping);
        return Result.Success();
    }

    public async Task<Result> DeactivateServicePincodeMappingAsync(Guid id)
    {
        var mapping = await _servicePincodeMappingRepository.GetByIdAsync(id);
        if (mapping is null)
        {
            return Result.Failure(Error.NotFound("Serviceability.MappingNotFound", "The specified mapping does not exist."));
        }

        mapping.Deactivate();
        await _servicePincodeMappingRepository.UpdateAsync(mapping);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> PinServicePincodeMappingAsync(Guid id)
    {
        var mapping = await _servicePincodeMappingRepository.GetByIdAsync(id);
        if (mapping is null)
        {
            return Result.Failure(Error.NotFound("Serviceability.MappingNotFound", "The specified mapping does not exist."));
        }

        mapping.Pin();
        await _servicePincodeMappingRepository.UpdateAsync(mapping);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> UnpinServicePincodeMappingAsync(Guid id)
    {
        var mapping = await _servicePincodeMappingRepository.GetByIdAsync(id);
        if (mapping is null)
        {
            return Result.Failure(Error.NotFound("Serviceability.MappingNotFound", "The specified mapping does not exist."));
        }

        mapping.Unpin();
        await _servicePincodeMappingRepository.UpdateAsync(mapping);
        return Result.Success();
    }

    public Task<IReadOnlyList<UnmappedActiveServiceResponse>> ListUnmappedActiveServicesAsync() =>
        _servicePincodeMappingRepository.ListUnmappedActiveServicesAsync();

    public Task<IReadOnlyList<ServiceabilityCoverageGapResponse>> ListPincodesWithProviderCoverageButNoServiceMappingAsync() =>
        _servicePincodeMappingRepository.ListPincodesWithProviderCoverageButNoServiceMappingAsync();

    /// <inheritdoc/>
    public async Task<int> AutoEnableProviderCoverageAsync(Guid providerId)
    {
        if (!await IsAutoManagementEnabledAsync())
        {
            return 0;
        }

        var coverable = await _servicePincodeMappingRepository.ListCoverablePairsForProviderAsync(providerId);
        if (coverable.Count == 0)
        {
            return 0;
        }

        var enabled = 0;
        var nowUtc = DateTime.UtcNow;
        foreach (var pair in coverable)
        {
            // Pin/cooldown only matter for a mapping that already exists -
            // ListCoverablePairsForProviderAsync only returns pairs with no
            // currently-active mapping, but an inactive (suspended) one may
            // still be sitting there pinned or mid-cooldown.
            var existing = await _servicePincodeMappingRepository.FindAsync(pair.ServiceId, pair.PincodeId);
            if (existing is not null && (existing.IsPinned || IsWithinAutoToggleCooldown(existing, nowUtc)))
            {
                continue;
            }

            // Reuses the same create-or-reactivate path the admin mapping
            // screen uses (see the interface doc comment) - never hand-rolls
            // persistence here, and inherits its idempotency: a pair already
            // actively mapped never reaches this loop (ListCoverablePairsForProviderAsync
            // excludes it), and a concurrent duplicate call just reactivates
            // the same row again rather than erroring or duplicating it.
            var result = await CreateServicePincodeMappingAsync(new ServicePincodeMappingCreateRequest(pair.ServiceId, pair.PincodeId));
            if (result.IsSuccess)
            {
                enabled++;
                await RecordAutoToggleAsync(result.Value.Id, "AutoEnabled", pair.ServiceId, pair.PincodeId, providerId, nowUtc);
            }
        }

        return enabled;
    }

    public Task<IReadOnlyList<ServiceabilityCoverageGapResponse>> ListMappedPairsCoveredByProviderAsync(Guid providerId) =>
        _servicePincodeMappingRepository.ListMappedPairsCoveredByProviderAsync(providerId);

    /// <inheritdoc/>
    public async Task<int> AutoDisableUnservedMappingsAsync(Guid providerId, IReadOnlyList<ServiceabilityCoverageGapResponse>? candidatePairs = null)
    {
        if (!await IsAutoManagementEnabledAsync())
        {
            return 0;
        }

        // No explicit snapshot supplied - this is the suspend/deactivate call
        // shape, where the provider's skill/area rows are untouched by the
        // status change, so "what they currently satisfy" IS the "before"
        // picture (see the interface doc comment).
        var candidates = candidatePairs ?? await _servicePincodeMappingRepository.ListMappedPairsCoveredByProviderAsync(providerId);
        if (candidates.Count == 0)
        {
            return 0;
        }

        var disabled = 0;
        var nowUtc = DateTime.UtcNow;
        foreach (var pair in candidates)
        {
            var mapping = await _servicePincodeMappingRepository.FindAsync(pair.ServiceId, pair.PincodeId);
            if (mapping is null || !mapping.IsActive || mapping.IsPinned)
            {
                continue;
            }

            // Re-checked against current state (after whatever change
            // triggered this call), not the snapshot - another active
            // provider may still cover this pair, or this same provider may
            // still cover it if only one of skill/area changed.
            if (await _servicePincodeMappingRepository.HasActiveProviderCoverageAsync(pair.ServiceId, pair.PincodeId))
            {
                // Coverage still (or again) holds - cancel any grace-period
                // timer a previous pass may have started for this pair.
                if (mapping.PendingAutoDisableSince is not null)
                {
                    mapping.ClearPendingAutoDisable();
                    await _servicePincodeMappingRepository.UpdateAsync(mapping);
                }

                continue;
            }

            if (IsWithinAutoToggleCooldown(mapping, nowUtc))
            {
                continue;
            }

            if (IsPastGracePeriod(mapping, nowUtc))
            {
                // Already pending from an earlier pass and the grace period
                // has fully elapsed since - coverage is still lost now, so
                // disable it for real.
                mapping.Deactivate();
                await RecordAutoToggleAsync(mapping, "AutoDisabled", pair.ServiceId, pair.PincodeId, providerId, nowUtc);
                disabled++;
            }
            else
            {
                // First time this pair is seen unserved (or still within its
                // grace period) - start/keep the pending timer rather than
                // disabling immediately, so a brief provider suspend/
                // reactivate blip does not take the pincode dark.
                mapping.MarkPendingAutoDisable(nowUtc);
                await _servicePincodeMappingRepository.UpdateAsync(mapping);
            }
        }

        return disabled;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MappedPincodeWithoutProviderCoverageResponse>> ListMappedPincodesWithoutProviderCoverageAsync() =>
        _servicePincodeMappingRepository.ListMappedPincodesWithoutProviderCoverageAsync();

    /// <inheritdoc/>
    public Task<IReadOnlyList<MappedPincodeWithActiveProviderCoverageResponse>> ListMappedPincodesWithActiveProviderCoverageAsync() =>
        _servicePincodeMappingRepository.ListMappedPincodesWithActiveProviderCoverageAsync();

    /// <inheritdoc/>
    public Task<IReadOnlyList<MappingCoveringProviderResponse>> ListActiveProvidersForMappingAsync(Guid mappingId) =>
        _servicePincodeMappingRepository.ListActiveProvidersForMappingAsync(mappingId);

    /// <summary>
    /// The <c>FeatureFlagSettings.AutoManageServiceabilityEnabled</c> kill
    /// switch. Fails open (treats the flag as enabled) when the settings
    /// store has no "features" row or cannot be read, rather than silently
    /// disabling the whole auto-management feature on an uninitialized or
    /// momentarily unreadable settings store - the safer default for a
    /// feature whose entire purpose is keeping serviceability in sync with
    /// live provider coverage.
    /// </summary>
    private async Task<bool> IsAutoManagementEnabledAsync()
    {
        var result = await _systemSettingsService.GetFeatureFlagSettingsAsync();
        return !result.IsSuccess || result.Value.AutoManageServiceabilityEnabled;
    }

    private static bool IsWithinAutoToggleCooldown(ServicePincodeMapping mapping, DateTime nowUtc) =>
        mapping.LastAutoToggledAtUtc is { } lastToggledAtUtc &&
        nowUtc - lastToggledAtUtc < TimeSpan.FromMinutes(ServiceabilityAutoManagementDefaults.AutoToggleCooldownMinutes);

    private static bool IsPastGracePeriod(ServicePincodeMapping mapping, DateTime nowUtc) =>
        mapping.PendingAutoDisableSince is { } pendingSinceUtc &&
        nowUtc - pendingSinceUtc >= TimeSpan.FromMinutes(ServiceabilityAutoManagementDefaults.AutoDisableGracePeriodMinutes);

    /// <summary>Loads the mapping by id and delegates to the entity overload - see its doc comment.</summary>
    private async Task RecordAutoToggleAsync(Guid mappingId, string action, Guid serviceId, Guid pincodeId, Guid providerId, DateTime toggledAtUtc)
    {
        var mapping = await _servicePincodeMappingRepository.GetByIdAsync(mappingId);
        if (mapping is null)
        {
            return;
        }

        await RecordAutoToggleAsync(mapping, action, serviceId, pincodeId, providerId, toggledAtUtc);
    }

    /// <summary>
    /// Common tail of every actual auto-enable/auto-disable toggle: stamps
    /// the flap-protection cooldown, writes the audit entry, then persists -
    /// in that order, so the audit row and the mapping's own change commit in
    /// the same transaction (<see cref="IAuditLogWriter.WriteAsync"/>'s
    /// documented contract). Attributed to <see cref="AuditContext.System"/>
    /// rather than whoever's request happened to trigger this - the toggle
    /// itself is a system decision computed from live provider coverage, not
    /// the human's (a provider replacing their own skills, or an admin
    /// reactivating them) - matching how <see cref="AuditActorType.System"/>
    /// already covers this codebase's other no-human-actor writes (a
    /// scheduled job, or - per <c>HttpAuditContextProvider</c>'s own doc
    /// comment - any write with no ambient HTTP request at all).
    /// </summary>
    private async Task RecordAutoToggleAsync(ServicePincodeMapping mapping, string action, Guid serviceId, Guid pincodeId, Guid providerId, DateTime toggledAtUtc)
    {
        mapping.RecordAutoToggle(toggledAtUtc);

        var newValues = JsonSerializer.Serialize(new
        {
            serviceId,
            pincodeId,
            triggeringProviderId = providerId,
        });

        await _auditLogWriter.WriteAsync(
            new AuditEntry("ServicePincodeMapping", mapping.Id.ToString(), action, NewValues: newValues),
            context: AuditContext.System);

        await _servicePincodeMappingRepository.UpdateAsync(mapping);
    }
}
