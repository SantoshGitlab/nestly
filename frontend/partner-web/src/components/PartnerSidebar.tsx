"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

/**
 * Sidebar nav for the partner portal. Unlike admin-web's AdminSidebar, there
 * is no role/permission model to filter by - every signed-in partner sees
 * the same fixed set of sections (docs/PARTNER.md's partner-facing API
 * surface: profile, availability, jobs, earnings).
 */
const NAV_ITEMS = [
  { key: "jobs", href: "/jobs", label: "Jobs" },
  { key: "availability", href: "/availability", label: "Availability" },
  { key: "earnings", href: "/earnings", label: "Earnings" },
  { key: "profile", href: "/profile", label: "Profile" },
] as const;

export function PartnerSidebar() {
  const pathname = usePathname();

  return (
    <nav
      aria-label="Partner sections"
      className="flex w-60 shrink-0 flex-col gap-1 border-r border-black/10 p-4 dark:border-white/15"
    >
      {NAV_ITEMS.map((item) => {
        const isActive = pathname === item.href || pathname.startsWith(`${item.href}/`);
        return (
          <Link
            key={item.key}
            href={item.href}
            aria-current={isActive ? "page" : undefined}
            className={`rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
              isActive
                ? "bg-black text-white dark:bg-white dark:text-black"
                : "hover:bg-black/5 dark:hover:bg-white/10"
            }`}
          >
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}
