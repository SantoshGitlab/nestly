"use client";

import { NavTabs } from "@/components/nav-tabs";

/**
 * Sub-nav between the customer directory and the Customer Analytics
 * dashboard - mirrors ProvidersTabs.tsx's own `_components/*Tabs.tsx`
 * pattern per admin module (CatalogTabs/ServiceabilityTabs/ProvidersTabs).
 */
export function CustomersTabs() {
  return (
    <NavTabs
      label="Customer sections"
      tabs={[
        // No matchPrefixes on "Directory": a blanket "/customers/" prefix
        // would also match "/customers/analytics" and the per-customer
        // detail route "/customers/[customerId]", highlighting more than
        // one tab (or the wrong one) at once - same reasoning as
        // ProvidersTabs' own "Directory" entry.
        { href: "/customers", label: "Directory" },
        { href: "/customers/analytics", label: "Analytics" },
      ]}
    />
  );
}
