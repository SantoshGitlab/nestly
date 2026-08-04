"use client";

import { NavTabs } from "@/components/nav-tabs";

/** Sub-nav between the three SRS 12.5-12.7 catalog management screens. */
export function CatalogTabs() {
  return (
    <NavTabs
      label="Catalog sections"
      tabs={[
        // "/catalog" also owns its nested category create/edit routes, which
        // live at "/catalog/categories/[id]" rather than under "/catalog"
        // directly — without the prefix they would highlight no tab at all.
        { href: "/catalog", label: "Categories", matchPrefixes: ["/catalog/categories"] },
        { href: "/catalog/services", label: "Services", matchPrefixes: ["/catalog/services"] },
        { href: "/catalog/addons", label: "Add-ons", matchPrefixes: ["/catalog/addons"] },
      ]}
    />
  );
}
