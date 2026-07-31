namespace Nestly.Domain;

/// <summary>
/// The fixed set of admin-configurable settings groups (SRS 12.19 "System
/// Configuration", tasks 131a-131g). Each group is one row in <c>system_setting</c>
/// (see <see cref="SystemSetting"/>) holding a typed settings payload as JSON -
/// one coherent store rather than seven separate ad-hoc tables, mirroring how
/// <see cref="AdminModules"/> is the fixed set the permission matrix is built
/// from. Every value here doubles as the group's <see cref="SystemSetting.GroupKey"/>.
/// </summary>
/// <remarks>
/// SRS 12.19 also lists "Communication provider settings", "Public contact
/// details" and general "Feature flags" as candidate groups. Those are not
/// backed by a lettered subtask (131a-131h only cover the seven groups below)
/// and have no existing consuming code to anchor a schema to, so adding them
/// now would be speculative (YAGNI, docs/CODING-STANDARDS.md). The store is
/// designed to extend to them later without a redesign: add a constant here,
/// a settings record in SettingsContracts.cs, a validator, and a seed row.
/// </remarks>
public static class SystemSettingGroups
{
    public const string Booking = "booking";
    public const string Slot = "slot";
    public const string Cancellation = "cancellation";
    public const string Reschedule = "reschedule";
    public const string Tax = "tax";
    public const string Wallet = "wallet";
    public const string Coupon = "coupon";

    /// <summary>Every settings group, in SRS 12.19 bullet order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Booking, Slot, Cancellation, Reschedule, Tax, Wallet, Coupon
    ];
}
