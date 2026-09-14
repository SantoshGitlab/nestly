using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain.Events;

namespace Nestly.Domain;

/// <summary>
/// Serviceability mapping: whether a service is active in a given pincode
/// (SRS 12.9.2). Pincode is the level service-serviceability is mapped
/// against; a locality's serviceability follows its parent pincode.
/// Deactivating a mapping is how admins apply a temporary suspension or
/// blackout for that service in that pincode, without deleting the record.
/// </summary>
public class ServicePincodeMapping : AggregateRoot<Guid>
{
    public Guid ServiceId { get; private set; }
    public Guid PincodeId { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Admin override flag (docs/OPEN-FIXES-FEATURES.csv "Service to pincode
    /// mapping" follow-up - "pin/override so admin decisions aren't silently
    /// overwritten"). When true, this mapping's active state is admin-owned:
    /// <c>IServiceabilityMappingManagementService.AutoEnableProviderCoverageAsync</c>
    /// and <c>AutoDisableUnservedMappingsAsync</c> both skip it entirely,
    /// leaving it exactly as an admin last set it regardless of live provider
    /// coverage. Defaults false - most mappings stay fully automatic.
    /// </summary>
    public bool IsPinned { get; private set; }

    /// <summary>
    /// When this mapping was last changed by auto-enable/auto-disable (not by
    /// an admin) - the flap-protection cooldown's clock. Null until the first
    /// auto-toggle. See <see cref="RecordAutoToggle"/> and
    /// <c>ServiceabilityAutoManagementDefaults.AutoToggleCooldownMinutes</c>.
    /// </summary>
    public DateTime? LastAutoToggledAtUtc { get; private set; }

    /// <summary>
    /// Set the moment auto-disable first observes this mapping has lost all
    /// active provider coverage; null while coverage holds or once the
    /// mapping has actually been auto-disabled. Auto-disable only acts once
    /// this has been in the past for at least
    /// <c>ServiceabilityAutoManagementDefaults.AutoDisableGracePeriodMinutes</c> -
    /// the grace period that keeps a brief provider suspend/reactivate blip
    /// from taking a pincode dark. See <see cref="MarkPendingAutoDisable"/>/
    /// <see cref="ClearPendingAutoDisable"/>.
    /// </summary>
    public DateTime? PendingAutoDisableSince { get; private set; }

    protected ServicePincodeMapping() { }

    public ServicePincodeMapping(Guid id, Guid serviceId, Guid pincodeId) : base(id)
    {
        ServiceId = serviceId;
        PincodeId = pincodeId;
        IsActive = true;
        RaiseDomainEvent(new ServicePincodeMappingChangedEvent(ServiceId, PincodeId));
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        RaiseDomainEvent(new ServicePincodeMappingChangedEvent(ServiceId, PincodeId));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        RaiseDomainEvent(new ServicePincodeMappingChangedEvent(ServiceId, PincodeId));
    }

    /// <summary>Admin marks this mapping's active state as their own to manage - auto-enable/auto-disable will skip it from now on.</summary>
    public void Pin() => IsPinned = true;

    /// <summary>Hands this mapping's active state back to auto-enable/auto-disable.</summary>
    public void Unpin() => IsPinned = false;

    /// <summary>
    /// Records that auto-enable/auto-disable just changed this mapping's
    /// active state (call alongside <see cref="Activate"/>/<see cref="Deactivate"/>,
    /// never for an admin-initiated change). Starts the flap-protection
    /// cooldown and clears any pending grace-period timer - the toggle just
    /// applied supersedes it either way.
    /// </summary>
    public void RecordAutoToggle(DateTime toggledAtUtc)
    {
        LastAutoToggledAtUtc = toggledAtUtc;
        PendingAutoDisableSince = null;
    }

    /// <summary>
    /// Starts the auto-disable grace-period timer the first time coverage is
    /// observed lost; a repeat call while already pending keeps the original
    /// (earliest) timestamp rather than resetting the clock.
    /// </summary>
    public void MarkPendingAutoDisable(DateTime observedAtUtc) => PendingAutoDisableSince ??= observedAtUtc;

    /// <summary>Coverage returned before the grace period elapsed - cancels the pending auto-disable.</summary>
    public void ClearPendingAutoDisable() => PendingAutoDisableSince = null;
}
