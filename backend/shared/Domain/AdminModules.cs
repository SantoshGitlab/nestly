namespace Nestly.Domain;

/// <summary>
/// The Phase 6 admin panel verticals (SRS section 12 — dashboard 12.3,
/// customers 12.4, and one module per remaining admin area). This is the
/// fixed set of modules the permission matrix (<see cref="AdminPermissionCatalog"/>,
/// SRS 12.2.3) is built from — every <see cref="AdminPermission.Module"/>
/// value is one of these constants, never a free-typed string.
/// </summary>
public static class AdminModules
{
    public const string Dashboard = "dashboard";
    public const string Customers = "customers";
    public const string Catalog = "catalog";
    public const string Pricing = "pricing";
    public const string Serviceability = "serviceability";
    public const string Slots = "slots";
    public const string Bookings = "bookings";
    public const string Coupons = "coupons";
    public const string Support = "support";
    public const string Reviews = "reviews";
    public const string Cms = "cms";
    public const string Notifications = "notifications";
    public const string Reports = "reports";
    public const string Audit = "audit";
    public const string Settings = "settings";

    /// <summary>
    /// Partner directory management: CRUD, KYC/background-check approval,
    /// suspend, performance view (PARTNER.md RBAC ADDITIONS, task 150c).
    /// </summary>
    public const string Partner = "partner";

    /// <summary>Partner payout batches: view, process (mark processing/paid/failed), approve (PARTNER.md RBAC ADDITIONS, task 150c).</summary>
    public const string Payout = "payout";

    /// <summary>Every module, in the order they appear in SRS section 12, followed by the Phase 7 Partner module additions (task 150c).</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Dashboard, Customers, Catalog, Pricing, Serviceability, Slots, Bookings,
        Coupons, Support, Reviews, Cms, Notifications, Reports, Audit, Settings,
        Partner, Payout
    ];
}
