"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const TABS = [
  { href: "/cms", label: "Pages" },
  { href: "/cms/banners", label: "Banners" },
  { href: "/cms/faqs", label: "FAQs" },
] as const;

/** Sub-nav between the three SRS 12.16 CMS screens: static pages, banners, and site-level FAQs (tasks 125a-125c). */
export function CmsTabs() {
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
