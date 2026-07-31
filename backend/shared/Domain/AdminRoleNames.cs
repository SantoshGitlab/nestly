namespace Nestly.Domain;

/// <summary>
/// The default admin roles seeded for the permission matrix (SRS 12.2.2).
/// Roles remain configurable at runtime — a Super Admin can define more via
/// <see cref="AdminRole"/> — these are only the fixed starting set task 96a
/// seeds so the platform is usable on day one.
/// </summary>
public static class AdminRoleNames
{
    public const string SuperAdmin = "Super Admin";
    public const string OperationsAdmin = "Operations Admin";
    public const string BookingAdmin = "Booking Admin";
    public const string SupportAdmin = "Support Admin";
    public const string CatalogAdmin = "Catalog Admin";
    public const string PricingAdmin = "Pricing Admin";
    public const string MarketingAdmin = "Marketing Admin";
    public const string FinanceAdmin = "Finance Admin";
    public const string ReadOnlyAnalyst = "Read-only Analyst";

    /// <summary>Every seeded role, in the order SRS 12.2.2 lists them.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        SuperAdmin, OperationsAdmin, BookingAdmin, SupportAdmin, CatalogAdmin,
        PricingAdmin, MarketingAdmin, FinanceAdmin, ReadOnlyAnalyst
    ];

    /// <summary>
    /// Short, human-readable purpose for each seeded role — shown in the
    /// admin permission-matrix UI and used to populate <see cref="AdminRole.Description"/>
    /// when the seed migration creates each row.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [SuperAdmin] = "Full access to every module, including admin user/role/permission management and the audit trail.",
        [OperationsAdmin] = "Day-to-day operational oversight: customers, serviceability, slots, bookings, support and notifications.",
        [BookingAdmin] = "Manages the booking lifecycle and the slot capacity it depends on.",
        [SupportAdmin] = "Handles support tickets and customer reviews.",
        [CatalogAdmin] = "Maintains the service catalog, serviceability coverage and CMS content describing them.",
        [PricingAdmin] = "Maintains pricing policy and the coupons that discount it.",
        [MarketingAdmin] = "Runs marketing campaigns: coupons, notifications and CMS content.",
        [FinanceAdmin] = "Reviews financial reporting and the audit trail behind it.",
        [ReadOnlyAnalyst] = "Read-only visibility across every module for analysis and reporting; cannot make changes."
    };
}
