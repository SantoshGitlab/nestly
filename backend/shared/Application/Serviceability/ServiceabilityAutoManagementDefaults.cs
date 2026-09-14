namespace Nestly.Application.Serviceability;

/// <summary>
/// Tunable constants for the service/pincode auto-enable/auto-disable safety
/// net (docs/OPEN-FIXES-FEATURES.csv "Service to pincode mapping" follow-up:
/// pin/override, flap protection, auto-disable grace period). Plain
/// constants rather than an <c>IOptions</c>-bound settings class - compare
/// <c>AutoAssignmentOptions.ResponseWindowMinutes</c>, which is config-bound
/// because operators have actually needed to tune it per environment; nobody
/// has asked to tune either value here yet, so binding them to configuration
/// now would be speculative (YAGNI, docs/CODING-STANDARDS.md). Promote either
/// to a real options class the first time that changes.
/// </summary>
public static class ServiceabilityAutoManagementDefaults
{
    /// <summary>
    /// Flap protection: once a mapping is auto-toggled (enabled or
    /// disabled), it is not auto-toggled again for this many minutes even if
    /// coverage flips back within the window - so two rapid coverage-loss-
    /// then-gain cycles toggle it at most once, not twice.
    /// </summary>
    public const int AutoToggleCooldownMinutes = 15;

    /// <summary>
    /// Grace period before an auto-disable actually takes effect. Coverage
    /// loss must still hold after this many minutes before the mapping is
    /// deactivated, so a brief provider suspend/reactivate blip does not take
    /// a pincode dark. Deliberately does not apply to auto-enable - enabling
    /// something that is genuinely coverable is always safe and should never
    /// be delayed.
    /// </summary>
    public const int AutoDisableGracePeriodMinutes = 30;
}
