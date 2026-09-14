import type { FeatureFlagSettings } from "@/lib/settings-types";

/**
 * Search corpus for the System Settings page (SRS 12.19), factored out of
 * `app/(admin)/settings/page.tsx` (task: admin-web global search) so the
 * same {@link SettingsSearchTerms} data can back two independent search UIs
 * without duplicating it: the settings page's own on-page field filter, and
 * the global command-palette search (`lib/search-index.ts`), which surfaces
 * each settings group - and, individually, every feature flag - as
 * "found under Settings" results reachable from any admin page.
 *
 * Nothing here is invented: every title/description/field string is the
 * same visible copy the settings page renders, just no longer duplicated
 * between the two call sites.
 */

/**
 * Case-insensitive substring match. An empty query matches everything (the
 * unfiltered, default state). Kept intentionally simple - no fuzzy matching,
 * no tokenization.
 */
export function matchesSearch(query: string, ...texts: string[]): boolean {
  const needle = query.trim().toLowerCase();
  if (!needle) return true;
  return texts.some((text) => text.toLowerCase().includes(needle));
}

/**
 * Per-settings-group search corpus: the card's own title/description plus
 * every field's visible label (and, where present, its description) - no
 * metadata invented beyond what's already rendered on the settings page.
 */
export interface SettingsSearchTerms {
  readonly title: string;
  readonly description: string;
  readonly fields: readonly string[];
}

export const BOOKING_SEARCH_TERMS: SettingsSearchTerms = {
  title: "Booking rules",
  description: "How far ahead, and how close to a slot, a booking may be created (SRS 12.19).",
  fields: [
    "Minimum lead time (hours)",
    "Max advance booking (days)",
    "Max active bookings per customer (blank = unlimited)",
    "Allow same-day booking",
  ],
};

export const SLOT_SEARCH_TERMS: SettingsSearchTerms = {
  title: "Slot rules",
  description: "How slots are generated and offered: duration, same-day cutoff, and capacity (SRS 15.2).",
  fields: [
    "Default slot duration (minutes)",
    "Same-day cutoff (hours)",
    "Max advance booking (days)",
    "Default slot capacity",
    "Allow overbooking",
  ],
};

export const CANCELLATION_SEARCH_TERMS: SettingsSearchTerms = {
  title: "Cancellation policy",
  description: "Free cancellation window and the late-cancellation fee (SRS 11.14.1).",
  fields: ["Free cancellation window (hours)", "Late cancellation fee (%)", "Allow admin override of the late fee"],
};

export const RESCHEDULE_SEARCH_TERMS: SettingsSearchTerms = {
  title: "Reschedule policy",
  description: "Reschedule blocking window, count limit, and the late-reschedule fee (SRS 11.15.1).",
  fields: [
    "Blocked within (hours before slot)",
    "Max reschedules per booking",
    "Late fee threshold (hours before slot)",
    "Late reschedule fee (%)",
  ],
};

export const TAX_SEARCH_TERMS: SettingsSearchTerms = {
  title: "Tax settings",
  description: "Default tax rate and registration details shown on customer invoices.",
  fields: [
    "Default tax rate (%)",
    "Tax registration number (blank = not configured)",
    "Displayed prices already include tax",
  ],
};

export const WALLET_SEARCH_TERMS: SettingsSearchTerms = {
  title: "Wallet settings",
  description: "Balance cap, how much of a booking wallet may cover, and credit expiry (SRS 14.5).",
  fields: [
    "Max wallet balance",
    "Max wallet usage per booking (%)",
    "Wallet credit expiry (days, blank = never)",
    "Allow customers to top up their wallet directly",
  ],
};

export const COUPONS_ENABLED_DESCRIPTION =
  "Feature flag: turning this off disables coupon redemption everywhere, regardless of individual coupon state.";

export const COUPON_SEARCH_TERMS: SettingsSearchTerms = {
  title: "Coupon settings",
  description: "Platform-wide guardrails applied across every coupon, plus the coupons feature flag (SRS 14.2).",
  fields: [
    "Max discount per coupon (%)",
    "Max active coupons per customer (blank = unlimited)",
    "Allow more than one coupon per booking",
    `Coupons enabled ${COUPONS_ENABLED_DESCRIPTION}`,
  ],
};

/**
 * Each flag's search text is `"<label> <description>"`, so e.g. typing "nav"
 * surfaces every flag whose *description* mentions a nav entry, not just
 * ones with "nav" in the short label. Also used, individually, as their own
 * global-search entries - see `lib/search-index.ts` - since "features and
 * options" are exactly what these toggles are.
 */
export const CUSTOMER_FLAGS = [
  { name: "walletEnabled", label: "Wallet", description: "Wallet nav entry, account-menu link and bottom-tab entry." },
  { name: "referralsEnabled", label: "Refer & earn", description: "Refer & Earn nav entry and page." },
  { name: "amcSubscriptionsEnabled", label: "AMC plans", description: "AMC Plans nav entry and promo card." },
  {
    name: "serviceRatingsEnabled",
    label: "Service rating badge",
    description: "Rating/review-count trust badge on the service detail page.",
  },
  {
    name: "bookingHelpLinkEnabled",
    label: "Booking help link",
    description: '"Need help? Contact support" link on the booking summary/payment pages.',
  },
] as const satisfies readonly { name: keyof FeatureFlagSettings; label: string; description: string }[];

export const PROVIDER_FLAGS = [
  {
    name: "ratingsPageEnabled",
    label: "Ratings & feedback",
    description: "Ratings promo card on Profile and the Ratings page.",
  },
  {
    name: "calendarViewEnabled",
    label: "Calendar view",
    description: "Week calendar entry points on Jobs/Availability and the Calendar page.",
  },
  {
    name: "earningsLedgerEnabled",
    label: "Earnings ledger",
    description: "Only the transaction ledger section of Earnings - summary, per-job earnings and payouts stay visible.",
  },
  {
    name: "offersScreenEnabled",
    label: "Offers screen",
    description: "The dedicated Offers screen. Accepting/declining an offer stays available from Today and Jobs either way.",
  },
] as const satisfies readonly { name: keyof FeatureFlagSettings; label: string; description: string }[];

export const PLATFORM_FLAGS = [
  {
    name: "autoManageServiceabilityEnabled",
    label: "Auto-manage serviceability",
    description:
      "Master kill switch for auto-enabling/auto-disabling service/pincode mappings from live provider coverage. Turning this off freezes every mapping's active state at whatever it is now, until an admin changes it by hand.",
  },
] as const satisfies readonly { name: keyof FeatureFlagSettings; label: string; description: string }[];

export const FEATURE_SEARCH_TERMS: SettingsSearchTerms = {
  title: "Feature flags",
  description:
    "Turn optional customer- and provider-facing features on or off. Core booking, payment and fulfilment steps are never affected (SRS 12.19).",
  fields: [...CUSTOMER_FLAGS, ...PROVIDER_FLAGS, ...PLATFORM_FLAGS].map((flag) => `${flag.label} ${flag.description}`),
};

/**
 * Every group's search corpus, for the settings page's own "nothing matched
 * anywhere" empty state and for building global-search entries.
 */
export const ALL_SETTINGS_SEARCH_TERMS: readonly SettingsSearchTerms[] = [
  BOOKING_SEARCH_TERMS,
  SLOT_SEARCH_TERMS,
  CANCELLATION_SEARCH_TERMS,
  RESCHEDULE_SEARCH_TERMS,
  TAX_SEARCH_TERMS,
  WALLET_SEARCH_TERMS,
  COUPON_SEARCH_TERMS,
  FEATURE_SEARCH_TERMS,
];

export function hasAnySettingsMatch(query: string): boolean {
  return ALL_SETTINGS_SEARCH_TERMS.some(
    (terms) => matchesSearch(query, terms.title, terms.description) || matchesSearch(query, ...terms.fields),
  );
}
