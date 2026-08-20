"use client";

import { NavTabs } from "@/components/nav-tabs";

/** Sub-nav across the provider-referral screens, mirrors (admin)/referral/_components/ReferralTabs.tsx (no reports tab in this v1 - see PROVIDER-REFERRAL.md). */
export function ProviderReferralTabs() {
  return (
    <NavTabs
      label="Provider referral sections"
      tabs={[
        { href: "/provider-referral", label: "Referrals & fraud queue" },
        { href: "/provider-referral/config", label: "Program config" },
      ]}
    />
  );
}
