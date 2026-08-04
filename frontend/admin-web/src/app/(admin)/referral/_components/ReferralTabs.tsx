"use client";

import { NavTabs } from "@/components/nav-tabs";

/**
 * Sub-nav across the three referral screens (tasks 167, 170, 171).
 *
 * These used to be a bare `<Link> · <Link>` sentence repeated at the top of
 * each page, with no indication of which one you were on.
 */
export function ReferralTabs() {
  return (
    <NavTabs
      label="Referral sections"
      tabs={[
        { href: "/referral", label: "Referrals & fraud queue" },
        { href: "/referral/config", label: "Program config" },
        { href: "/referral/reports", label: "Reports" },
      ]}
    />
  );
}
