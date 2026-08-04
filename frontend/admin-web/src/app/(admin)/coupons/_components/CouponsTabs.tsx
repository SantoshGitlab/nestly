"use client";

import { NavTabs } from "@/components/nav-tabs";

/** Sub-nav between the two SRS 12.12 screens: 12.12.1 coupon management and 12.12.2 redemption reporting. */
export function CouponsTabs() {
  return (
    <NavTabs
      label="Coupon sections"
      tabs={[
        { href: "/coupons", label: "Manage coupons" },
        { href: "/coupons/redemptions", label: "Redemption report" },
      ]}
    />
  );
}
