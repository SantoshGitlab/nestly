"use client";

import { NavTabs } from "@/components/nav-tabs";

/**
 * Sub-nav between the Customer Analytics dashboard and the customer
 * directory - mirrors ProvidersTabs.tsx's own `_components/*Tabs.tsx`
 * pattern per admin module (CatalogTabs/ServiceabilityTabs/ProvidersTabs).
 * Analytics is listed first and owns the module's root (`/customers`) since
 * it's the dashboard an admin actually wants to land on - the directory
 * moved to `/customers/directory` to make room (same Overview-first landing
 * as ProvidersTabs).
 */
export function CustomersTabs() {
  return (
    <NavTabs
      label="Customer sections"
      tabs={[
        // No matchPrefixes on "Analytics": pathname equality alone is
        // enough (NavTabs) - "/customers/directory" and the per-customer
        // detail route "/customers/[customerId]" are distinct strings, and
        // the detail page does not render this strip at all.
        { href: "/customers", label: "Analytics" },
        { href: "/customers/directory", label: "Directory" },
      ]}
    />
  );
}
