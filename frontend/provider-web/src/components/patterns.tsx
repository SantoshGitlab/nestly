"use client";

import type { ReactNode } from "react";
import { cx } from "@/components/ui";

/**
 * Screen-level patterns that don't belong in the frozen `components/ui.tsx`
 * kit (byte-identical across customer-web, admin-web and provider-web - see
 * that file's own header comment). Mirrors customer-web's
 * `components/patterns.tsx`, trimmed to what provider-web actually needs:
 * this app has no price breakdown, booking timeline or catalog pickers, but
 * it has exactly the same "primary action must survive scrolling on a phone"
 * problem customer-web's `StickyActionBar` was built for (Phase 22 mobile-
 * first pass, task #345).
 */

/**
 * Commit affordance that is inline on desktop and pinned to the bottom of the
 * viewport on mobile - the job detail screen's Accept/Start/Complete/Submit
 * actions must stay reachable regardless of how far down a provider has
 * scrolled through the job's details, chat thread or completion checklist.
 *
 * Safe to nest inside a `Card`: `position: fixed` escapes a `Card`'s
 * `overflow-hidden` because neither establishes a containing block for it
 * (no transform/filter/perspective) - the fixed box lays out against the
 * viewport, not the card, on mobile, and simply renders in normal flow
 * `md:static` and up.
 *
 * Renders its children exactly once - a second, separately-rendered mobile
 * copy would duplicate every accessible name on the page. Pair it with
 * `STICKY_BAR_SPACER` on the page's scroll container so the bar never covers
 * content at the end of a scroll.
 */
export function StickyActionBar({ children }: { children: ReactNode }) {
  return (
    <div
      className={cx(
        "fixed inset-x-0 bottom-0 z-40 flex flex-col gap-2 border-t border-line bg-surface/95 px-4 py-3 shadow-lg backdrop-blur",
        "supports-[padding:max(0px)]:pb-[max(0.75rem,env(safe-area-inset-bottom))]",
        "md:static md:z-auto md:flex-col md:gap-3 md:border-0 md:bg-transparent md:p-0 md:shadow-none md:backdrop-blur-none",
      )}
    >
      {children}
    </div>
  );
}

/**
 * Bottom padding a page needs so `StickyActionBar` never covers its last
 * row. Taller than the tab bar's own `pb-24` (see `ProviderTabBar`) because a
 * `StickyActionBar` can hold two stacked `lg` buttons (e.g. "On my way" +
 * "Start job") plus the safe-area inset, not one row of tab icons.
 */
export const STICKY_BAR_SPACER = "pb-40 md:pb-6";
