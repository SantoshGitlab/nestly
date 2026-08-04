"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cx } from "@/components/ui";

/**
 * Route-backed sub-navigation (task 222).
 *
 * Four modules — catalog, cms, coupons, serviceability — each carried their own
 * verbatim copy of this component, all four styled with raw `black/10`,
 * `neutral-*` and `border-black`/`dark:border-white` classes that predate the
 * token layer. This is that one strip, on tokens, matching `ui.tsx`'s `Tabs`
 * treatment so a link-driven tab and a state-driven tab look identical.
 *
 * It is a `<nav>` of real `<Link>`s rather than `role="tablist"`: each tab is a
 * separate route, so these must stay middle-clickable and must not claim the
 * arrow-key semantics a real tablist promises.
 */

export interface NavTab {
  href: string;
  label: string;
  /**
   * Also treat these path prefixes as this tab. For a tab whose nested detail
   * routes live outside its own href, e.g. `/catalog` owning
   * `/catalog/categories/[id]`.
   */
  matchPrefixes?: readonly string[];
}

export function NavTabs({
  tabs,
  label,
  className = "",
}: {
  tabs: readonly NavTab[];
  /** Accessible name for the strip, e.g. "Catalog sections". */
  label: string;
  className?: string;
}) {
  const pathname = usePathname();

  return (
    <nav aria-label={label} className={cx("mb-6", className)}>
      <div className="flex gap-1 overflow-x-auto border-b border-line">
        {tabs.map((tab) => {
          const isActive =
            pathname === tab.href ||
            (tab.matchPrefixes ?? []).some((prefix) => pathname.startsWith(prefix));

          return (
            <Link
              key={tab.href}
              href={tab.href}
              aria-current={isActive ? "page" : undefined}
              className={cx(
                "relative whitespace-nowrap px-4 py-2.5 text-sm font-medium transition-colors duration-fast ease-out",
                isActive ? "text-brand-600 dark:text-brand-400" : "text-fg-muted hover:text-fg",
              )}
            >
              {tab.label}
              {isActive ? (
                // Sits on the container's border so the indicator reads as part
                // of the rule rather than floating above it.
                <span className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-brand-600 dark:bg-brand-400" />
              ) : null}
            </Link>
          );
        })}
      </div>
    </nav>
  );
}
