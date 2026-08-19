"use client";

import { NavTabs } from "@/components/nav-tabs";

/**
 * Sub-nav shared by Subscription Plans and AMC Plans (docs/AMC.md RBAC
 * ADDITIONS): both are commercial catalog config gated behind the same
 * "subscription.read"/"subscription.write" tier, and AMC plans deliberately
 * get no `NavModule`/sidebar entry of their own - the RBAC doc's reasoning is
 * that an AMC contract (and by extension its plan catalog) is "a commercial
 * record adjacent to Subscription, not a new vertical."
 *
 * Lives at the top level, unlike `BookingsTabs`/`ReferralTabs`, because its
 * two tabs point at two different top-level route directories
 * (`/subscription-plans` and `/amc/plans`) rather than a parent route and its
 * own children - there is no single owning `_components` folder for both.
 */
export function SubscriptionTabs() {
  return (
    <NavTabs
      label="Subscription sections"
      tabs={[
        { href: "/subscription-plans", label: "Subscription plans" },
        { href: "/amc/plans", label: "AMC plans" },
      ]}
    />
  );
}
