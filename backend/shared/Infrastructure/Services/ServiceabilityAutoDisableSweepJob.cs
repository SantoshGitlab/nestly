using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Serviceability;
using Nestly.Application.Settings;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IServiceabilityAutoDisableSweepJob"/>.</summary>
public class ServiceabilityAutoDisableSweepJob : IServiceabilityAutoDisableSweepJob
{
    private readonly IServicePincodeMappingRepository _servicePincodeMappingRepository;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly ILogger<ServiceabilityAutoDisableSweepJob> _logger;

    public ServiceabilityAutoDisableSweepJob(
        IServicePincodeMappingRepository servicePincodeMappingRepository,
        IAuditLogWriter auditLogWriter,
        ISystemSettingsService systemSettingsService,
        ILogger<ServiceabilityAutoDisableSweepJob> logger)
    {
        _servicePincodeMappingRepository = servicePincodeMappingRepository;
        _auditLogWriter = auditLogWriter;
        _systemSettingsService = systemSettingsService;
        _logger = logger;
    }

    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        var flags = await _systemSettingsService.GetFeatureFlagSettingsAsync(cancellationToken);
        // Fails open, same as ServiceabilityMappingManagementService's own
        // kill-switch check - see its doc comment for why.
        if (flags.IsSuccess && !flags.Value.AutoManageServiceabilityEnabled)
        {
            _logger.LogInformation(
                "Serviceability auto-disable sweep is disabled (FeatureFlagSettings.AutoManageServiceabilityEnabled); no mappings were re-checked.");
            return 0;
        }

        var nowUtc = DateTime.UtcNow;
        var cutoffUtc = nowUtc.AddMinutes(-ServiceabilityAutoManagementDefaults.AutoDisableGracePeriodMinutes);
        var due = await _servicePincodeMappingRepository.ListPendingAutoDisableDueByAsync(cutoffUtc);

        var disabled = 0;
        var cleared = 0;
        foreach (var mapping in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // An admin may have pinned this mapping, or deactivated it by
            // hand, since the pending timer was set - both mean this pass
            // has nothing to do here beyond dropping the now-meaningless
            // timer.
            if (mapping.IsPinned || !mapping.IsActive)
            {
                mapping.ClearPendingAutoDisable();
                await _servicePincodeMappingRepository.UpdateAsync(mapping);
                continue;
            }

            var stillUnserved = !await _servicePincodeMappingRepository.HasActiveProviderCoverageAsync(mapping.ServiceId, mapping.PincodeId);
            if (!stillUnserved)
            {
                // Coverage returned since the pending timer was set - cancel it.
                mapping.ClearPendingAutoDisable();
                await _servicePincodeMappingRepository.UpdateAsync(mapping);
                cleared++;
                continue;
            }

            mapping.Deactivate();
            mapping.RecordAutoToggle(nowUtc);

            var newValues = JsonSerializer.Serialize(new { serviceId = mapping.ServiceId, pincodeId = mapping.PincodeId });
            await _auditLogWriter.WriteAsync(
                new AuditEntry("ServicePincodeMapping", mapping.Id.ToString(), "AutoDisabled", NewValues: newValues),
                cancellationToken,
                context: AuditContext.System);

            await _servicePincodeMappingRepository.UpdateAsync(mapping);
            disabled++;
        }

        if (disabled > 0 || cleared > 0)
        {
            _logger.LogInformation(
                "Serviceability auto-disable sweep: {DisabledCount} mapping(s) disabled past their grace period, {ClearedCount} pending timer(s) cleared (coverage returned).",
                disabled, cleared);
        }

        return disabled;
    }
}
