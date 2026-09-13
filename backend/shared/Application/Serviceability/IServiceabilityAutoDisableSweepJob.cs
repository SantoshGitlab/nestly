namespace Nestly.Application.Serviceability;

/// <summary>
/// Periodic, provider-independent backstop for
/// <c>IServiceabilityMappingManagementService.AutoDisableUnservedMappingsAsync</c>'s
/// grace period (docs/OPEN-FIXES-FEATURES.csv "Service to pincode mapping"
/// follow-up: "auto-disable should not fire the instant coverage is lost").
///
/// <para>
/// <c>AutoDisableUnservedMappingsAsync</c> only re-checks a mapping pending
/// disable when something touches that exact (service, pincode) pair again -
/// another skill/area change, another provider status flip. A pincode with
/// exactly one provider that goes quiet forever after the coverage-losing
/// event would otherwise sit <see cref="Nestly.Domain.ServicePincodeMapping.PendingAutoDisableSince"/>-pending
/// indefinitely, never actually disabled. This job is the periodic pass
/// that guarantees every pending mapping is revisited on a schedule, not
/// only opportunistically - registered as a Hangfire recurring job the same
/// way <c>IBookingFulfilmentPromotionJob</c> is (reusing this codebase's
/// existing recurring-job infrastructure rather than inventing a second
/// scheduling mechanism).
/// </para>
/// </summary>
public interface IServiceabilityAutoDisableSweepJob
{
    /// <summary>
    /// Re-checks every mapping whose grace period
    /// (<c>ServiceabilityAutoManagementDefaults.AutoDisableGracePeriodMinutes</c>)
    /// has fully elapsed: still unserved means it is disabled now (subject to
    /// the same pin/kill-switch checks as every other auto-disable path, and
    /// audited the same way); coverage restored means the pending timer is
    /// simply cleared. Idempotent and safe to re-run, per Hangfire's retry
    /// convention.
    /// </summary>
    /// <returns>How many mappings this pass actually disabled.</returns>
    Task<int> SweepAsync(CancellationToken cancellationToken = default);
}
