import type { AdminSessionClaims } from "./types";

/**
 * The admin panel's module list and permission-based nav filtering (SRS 12,
 * task 98c).
 *
 * Every module in SRS section 12 gets a nav entry here now, even though most
 * of their pages do not exist yet - each is a separate, not-yet-implemented
 * task. Linking to the expected route today means those tasks only need to
 * add `src/app/(admin)/<module>/page.tsx`; nothing here has to change.
 *
 * Permission model: `requiredPermission` follows the `module:action` shape
 * SRS 12.2.3 describes ("configurable at least by module/action"). The
 * real permission matrix (task 96a) has not landed yet, so
 * `AdminSessionClaims.permissions` may legitimately be empty for every admin
 * today - that is not the same thing as "no permissions granted". Until the
 * backend is populating that claim, nav visibility falls back to the
 * `ROLE_MODULE_FALLBACK` matrix below, keyed on the role claim SRS 12.1
 * guarantees exists. This fallback is a UX convenience, not a security
 * boundary: every one of these routes still requires its own valid
 * [Authorize] call server-side, exactly like RequireAdminAuth's client-side
 * guard is not itself the security boundary either. Once task 96a/a
 * permissions claim is present and non-empty, it takes over automatically -
 * no changes needed here.
 */

export type NavModuleKey =
  | "dashboard"
  | "customers"
  | "catalog"
  | "pricing"
  | "serviceability"
  | "slots"
  | "bookings"
  | "coupons"
  | "support"
  | "reviews"
  | "cms"
  | "notifications"
  | "reports"
  | "audit"
  | "settings";

export interface NavModule {
  key: NavModuleKey;
  label: string;
  href: string;
  /** SRS section this module corresponds to, for traceability. */
  srsRef: string;
  requiredPermission: string;
}

export const NAV_MODULES: readonly NavModule[] = [
  { key: "dashboard", label: "Dashboard", href: "/dashboard", srsRef: "SRS 12.3", requiredPermission: "dashboard:view" },
  { key: "customers", label: "Customers", href: "/customers", srsRef: "SRS 12.4", requiredPermission: "customers:view" },
  { key: "catalog", label: "Catalog", href: "/catalog", srsRef: "SRS 12.5-12.7", requiredPermission: "catalog:view" },
  { key: "pricing", label: "Pricing", href: "/pricing", srsRef: "SRS 12.8", requiredPermission: "pricing:view" },
  { key: "serviceability", label: "Serviceability", href: "/serviceability", srsRef: "SRS 12.9", requiredPermission: "serviceability:view" },
  { key: "slots", label: "Slots & Availability", href: "/slots", srsRef: "SRS 12.10", requiredPermission: "slots:view" },
  { key: "bookings", label: "Bookings", href: "/bookings", srsRef: "SRS 12.11, 12.13", requiredPermission: "bookings:view" },
  { key: "coupons", label: "Coupons & Campaigns", href: "/coupons", srsRef: "SRS 12.12", requiredPermission: "coupons:view" },
  { key: "support", label: "Support Tickets", href: "/support", srsRef: "SRS 12.14", requiredPermission: "support:view" },
  { key: "reviews", label: "Review Moderation", href: "/reviews", srsRef: "SRS 12.15", requiredPermission: "reviews:view" },
  { key: "cms", label: "CMS & Content", href: "/cms", srsRef: "SRS 12.16", requiredPermission: "cms:view" },
  { key: "notifications", label: "Notification Templates", href: "/notifications", srsRef: "SRS 12.17", requiredPermission: "notifications:view" },
  { key: "reports", label: "Reports & Exports", href: "/reports", srsRef: "SRS 12.18", requiredPermission: "reports:view" },
  { key: "audit", label: "Audit Log", href: "/audit", srsRef: "SRS 12.1.2, 12.2.3", requiredPermission: "audit:view" },
  { key: "settings", label: "System Settings", href: "/settings", srsRef: "SRS 12.19", requiredPermission: "settings:view" },
];

/**
 * Provisional role -> module matrix, standing in for the real permission
 * matrix (task 96a) until that lands. Derived from the role list and
 * responsibilities implied in SRS 12.2.2; every role gets the dashboard.
 * "Super Admin" is treated as unrestricted, matching its description as the
 * role that manages every other admin user and role (SRS 12.2.1).
 */
const ROLE_MODULE_FALLBACK: Record<string, NavModuleKey[] | "*"> = {
  "Super Admin": "*",
  "Operations Admin": ["dashboard", "customers", "bookings", "serviceability", "slots", "support"],
  "Booking Admin": ["dashboard", "bookings", "slots", "serviceability"],
  "Support Admin": ["dashboard", "support", "customers", "reviews"],
  "Catalog Admin": ["dashboard", "catalog", "pricing"],
  "Pricing Admin": ["dashboard", "pricing", "coupons"],
  "Marketing Admin": ["dashboard", "coupons", "cms", "notifications", "reviews"],
  "Finance Admin": ["dashboard", "bookings", "reports"],
  "Read-only Analyst": ["dashboard", "reports"],
};

function isModuleVisibleByRole(role: string | null, key: NavModuleKey): boolean {
  if (!role) return key === "dashboard";
  const allowed = ROLE_MODULE_FALLBACK[role];
  if (allowed === "*") return true;
  if (!allowed) return key === "dashboard"; // unrecognised role: safest usable default
  return allowed.includes(key);
}

/**
 * Returns the nav modules the given admin session should see.
 *
 * `claims` is null while the session is still loading/unauthenticated - no
 * modules are visible then (RequireAdminAuth keeps that state off-screen
 * anyway, but this stays defensive rather than assuming a caller order).
 */
export function getVisibleNavModules(claims: AdminSessionClaims | null): NavModule[] {
  if (!claims) return [];

  if (claims.permissions.length > 0) {
    const granted = new Set(claims.permissions);
    return NAV_MODULES.filter((module) => granted.has(module.requiredPermission));
  }

  return NAV_MODULES.filter((module) => isModuleVisibleByRole(claims.role, module.key));
}
