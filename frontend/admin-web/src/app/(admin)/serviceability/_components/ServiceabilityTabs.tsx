"use client";

import { NavTabs } from "@/components/nav-tabs";

/** Sub-nav between the two SRS 12.9 screens: 12.9.1 geography master and 12.9.2 mapping. */
export function ServiceabilityTabs() {
  return (
    <NavTabs
      label="Serviceability sections"
      tabs={[
        { href: "/serviceability", label: "Geography master" },
        { href: "/serviceability/mappings", label: "Serviceability mapping" },
      ]}
    />
  );
}
