"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const TABS = [
  { href: "/catalog", label: "Categories" },
  { href: "/catalog/services", label: "Services" },
  { href: "/catalog/addons", label: "Add-ons" },
] as const;

/** Sub-nav between the three SRS 12.5-12.7 catalog management screens. */
export function CatalogTabs() {
  const pathname = usePathname();

  return (
    <div className="mb-6 flex gap-2 border-b border-black/10 dark:border-white/15">
      {TABS.map((tab) => {
        // Exact match for "/catalog" itself; prefix match for its nested
        // create/edit routes (e.g. "/catalog/categories/[id]" should still
        // highlight "Categories") without "/catalog" itself matching every tab.
        const isActive =
          tab.href === "/catalog"
            ? pathname === "/catalog" || pathname.startsWith("/catalog/categories")
            : pathname.startsWith(tab.href);

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
