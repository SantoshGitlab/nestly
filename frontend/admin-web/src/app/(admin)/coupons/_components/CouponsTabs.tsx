"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const TABS = [
  { href: "/coupons", label: "Manage Coupons" },
  { href: "/coupons/redemptions", label: "Redemption Report" },
] as const;

/** Sub-nav between the two SRS 12.12 screens: 12.12.1 coupon management and 12.12.2 redemption reporting. */
export function CouponsTabs() {
  const pathname = usePathname();

  return (
    <div className="mb-6 flex gap-2 border-b border-black/10 dark:border-white/15">
      {TABS.map((tab) => {
        const isActive = pathname === tab.href;
        return (
          <Link
            key={tab.href}
            href={tab.href}
            aria-current={isActive ? "page" : undefined}
            className={`-mb-px border-b-2 px-3 py-2 text-sm font-medium transition-colors ${
              isActive
                ? "border-black text-black dark:border-white dark:text-white"
                : "border-transparent text-neutral-500 hover:text-black dark:text-neutral-400 dark:hover:text-white"
            }`}
          >
            {tab.label}
          </Link>
        );
      })}
    </div>
  );
}
