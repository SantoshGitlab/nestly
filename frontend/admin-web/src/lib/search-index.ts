import { ALL_SETTINGS_SEARCH_TERMS, CUSTOMER_FLAGS, PLATFORM_FLAGS, PROVIDER_FLAGS } from "@/lib/settings-search-terms";
import { getVisibleNavModules, type NavModuleKey } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";

/**
 * Global admin-web search (find any page, or any feature/option within a
 * page, from anywhere in the app - not the `/settings`-only search box
 * `settings/page.tsx` has of its own).
 *
 * One flat, static index built from real data already in the codebase, not
 * invented content:
 *  - every navigable page comes straight from `permissions.ts`'s
 *    `NAV_MODULES`, filtered by `getVisibleNavModules` - the exact function
 *    `AdminSidebar` itself calls, so a page only ever appears in search
 *    results when it would also appear in the sidebar for this admin.
 *  - every settings group and feature flag comes from
 *    `settings-search-terms.ts`, shared with the settings page's own
 *    in-page search so the two never drift apart.
 *  - every module's own sub-page ("tab") - e.g. "Coverage gap map" under
 *    Serviceability, "Reconciliation" under Payments - is read off that
 *    module's `*Tabs.tsx` component (`components/nav-tabs.tsx`'s `NavTab`
 *    shape), the same route list already rendered as that module's own
 *    in-page sub-nav. Listed as data below rather than imported from each
 *    `*Tabs.tsx` (none of those files export their tab array today) - kept
 *    gated on the same module visibility as the page they live under, so a
 *    sub-page never surfaces in search for an admin who cannot see its
 *    parent module.
 */

export type SearchCategory = "Pages" | "Settings";

export interface SearchIndexEntry {
  label: string;
  description?: string;
  category: SearchCategory;
  href: string;
  /**
   * Extra terms to match against that are not shown in the result row by
   * default - kept as individual strings (not one joined blob) so a match
   * that lands here rather than in the visible label/description can still
   * be attributed to a specific one and surfaced as a "matched: …" hint
   * (see {@link matchEntry}). A query that only matches here would otherwise
   * make a result look wrong - "Slot rules" for "book" is correct (it has a
   * "Max advance booking (days)" field) but inexplicable without this.
   */
  keywords?: readonly string[];
}

/**
 * One entry per module sub-page, sourced from that module's `*Tabs.tsx`
 * (see this file's doc comment) - every one of these labels/hrefs is copied
 * verbatim from the corresponding `NavTabs` call, minus the tab that just
 * points back at the module's own top-level href (already covered by the
 * `NAV_MODULES` page entry).
 */
const SUB_PAGE_ENTRIES: readonly { label: string; href: string; moduleKey: NavModuleKey }[] = [
  // BookingsTabs
  { label: "Unassigned & at-risk bookings", href: "/bookings/unassigned-at-risk", moduleKey: "bookings" },
  { label: "Recurring booking plans", href: "/bookings/recurring-plans", moduleKey: "bookings" },
  { label: "Booking assignment conflicts", href: "/bookings/conflicts", moduleKey: "bookings" },
  { label: "AMC contracts", href: "/amc/contracts", moduleKey: "bookings" },
  { label: "AMC renewal report", href: "/amc/renewal-report", moduleKey: "bookings" },
  // CatalogTabs
  { label: "Category groups", href: "/catalog/category-groups", moduleKey: "catalog" },
  { label: "Services", href: "/catalog/services", moduleKey: "catalog" },
  { label: "Service groups", href: "/catalog/service-groups", moduleKey: "catalog" },
  { label: "Add-ons", href: "/catalog/addons", moduleKey: "catalog" },
  { label: "Add-on groups", href: "/catalog/addon-groups", moduleKey: "catalog" },
  { label: "Catalog health", href: "/catalog/health", moduleKey: "catalog" },
  // CmsTabs
  { label: "CMS banners", href: "/cms/banners", moduleKey: "cms" },
  { label: "Site FAQs", href: "/cms/faqs", moduleKey: "cms" },
  // CouponsTabs
  { label: "Coupon redemption report", href: "/coupons/redemptions", moduleKey: "coupons" },
  // PaymentsTabs
  { label: "Payment reconciliation", href: "/payments/reconciliation", moduleKey: "payments" },
  // ProvidersTabs
  { label: "Provider performance", href: "/providers/performance", moduleKey: "provider" },
  // ProviderReferralTabs
  { label: "Provider referral program config", href: "/provider-referral/config", moduleKey: "provider-referral" },
  // ReferralTabs
  { label: "Referral program config", href: "/referral/config", moduleKey: "referral" },
  { label: "Referral reports", href: "/referral/reports", moduleKey: "referral" },
  // ServiceabilityTabs
  { label: "Serviceability mapping", href: "/serviceability/mappings", moduleKey: "serviceability" },
  { label: "Coverage gap map", href: "/serviceability/coverage-gaps", moduleKey: "serviceability" },
  // SubscriptionTabs - AMC plans shares "subscription.read"/write, no
  // NavModule of its own (see SubscriptionTabs' doc comment).
  { label: "AMC plans", href: "/amc/plans", moduleKey: "subscription" },
];

function buildSettingsEntries(): SearchIndexEntry[] {
  const groupEntries: SearchIndexEntry[] = ALL_SETTINGS_SEARCH_TERMS.map((terms) => ({
    label: terms.title,
    description: terms.description,
    category: "Settings",
    href: "/settings",
    keywords: terms.fields,
  }));

  // Named individually too (not just folded into "Feature flags" above) -
  // "features and options" is exactly what these toggles are, and each is
  // independently useful to find by its own label/description.
  const flagEntries: SearchIndexEntry[] = [...CUSTOMER_FLAGS, ...PROVIDER_FLAGS, ...PLATFORM_FLAGS].map((flag) => ({
    label: flag.label,
    description: flag.description,
    category: "Settings",
    href: "/settings",
    keywords: ["feature flag", "toggle", "setting"],
  }));

  return [...groupEntries, ...flagEntries];
}

/**
 * Builds the full global search index for the given admin session,
 * respecting the same nav-module visibility rule `AdminSidebar` renders by
 * (see `getVisibleNavModules`'s own doc comment for the permission/role
 * fallback it implements) - a page or sub-page this admin cannot open never
 * appears as a search result either.
 */
export function buildSearchIndex(claims: AdminSessionClaims | null): SearchIndexEntry[] {
  const visibleModules = getVisibleNavModules(claims);
  const visibleKeys = new Set(visibleModules.map((module) => module.key));

  const pageEntries: SearchIndexEntry[] = visibleModules.map((module) => ({
    label: module.label,
    category: "Pages",
    href: module.href,
  }));

  const subPageEntries: SearchIndexEntry[] = SUB_PAGE_ENTRIES.filter((entry) => visibleKeys.has(entry.moduleKey)).map(
    (entry) => ({
      label: entry.label,
      category: "Pages",
      href: entry.href,
    }),
  );

  return [...pageEntries, ...subPageEntries, ...buildSettingsEntries()];
}

export interface EntryMatch {
  matched: boolean;
  /**
   * The specific hidden keyword the query matched, when the match came from
   * `keywords` rather than the visible label/description - null whenever the
   * label or description alone already explains the result, or when nothing
   * matched at all. `GlobalSearch` shows this as a "Matched: …" hint so a
   * result like "Slot rules" for the query "book" (it has no "book" in its
   * own title or description, only a "Max advance booking (days)" field)
   * doesn't look like a broken/unrelated result.
   */
  matchedKeyword: string | null;
}

/** Case-insensitive substring match against an entry's visible text plus its hidden keywords. Same "simple, no fuzzy-matching" rule as the settings page's own search. */
export function matchEntry(entry: SearchIndexEntry, query: string): EntryMatch {
  const needle = query.trim().toLowerCase();
  if (!needle) return { matched: false, matchedKeyword: null };

  const visibleMatch = [entry.label, entry.description]
    .filter((text): text is string => Boolean(text))
    .some((text) => text.toLowerCase().includes(needle));
  if (visibleMatch) return { matched: true, matchedKeyword: null };

  const matchedKeyword = (entry.keywords ?? []).find((keyword) => keyword.toLowerCase().includes(needle));
  return matchedKeyword !== undefined
    ? { matched: true, matchedKeyword }
    : { matched: false, matchedKeyword: null };
}
