"use client";

import type { ReactNode } from "react";
import { Alert, Button, EmptyState, Skeleton } from "@/components/ui";
import { describeError } from "@/lib/api";

/**
 * The screen-level states every data-backed provider screen needs.
 *
 * These are provider-web-specific rather than candidates for components/ui.tsx:
 * that kit is byte-identical across all three frontends and has no shared
 * package behind it, so anything added there has to be ported three ways.
 * `NotYetAvailable` also encodes this app's HTTP 501 contract with provider-api,
 * which the other two frontends have no equivalent of, and `ErrorState` /
 * `ScreenSkeleton` are compositions over the kit's `Alert` and `Skeleton`
 * rather than new primitives.
 */

/**
 * Standard "this screen is loading" placeholder: a heading block plus a
 * configurable number of card-shaped blocks, sized to the real cards so the
 * page does not reflow when the data lands.
 *
 * Every provider screen is a heading over stacked `Card`s, so this is the shape
 * that actually gets replaced — which a bare "Loading…" line is not.
 */
export function ScreenSkeleton({
  cards = 3,
  className = "mx-auto w-full max-w-4xl px-4 py-8 sm:px-6 sm:py-10",
  children,
}: {
  cards?: number;
  className?: string;
  /**
   * Optional live-region line above the placeholder, for a wait with a reason
   * worth announcing (a redirect in flight) — the `aria-hidden` blocks below
   * cannot carry that on their own.
   */
  children?: ReactNode;
}) {
  return (
    <main className={className}>
      {children ? <div className="mb-6">{children}</div> : null}
      <div aria-hidden>
        <Skeleton className="h-8 w-56" />
        <Skeleton className="mt-3 h-4 w-72" />
        <div className="mt-8 flex flex-col gap-4">
          {Array.from({ length: cards }, (_, index) => (
            <Skeleton key={index} className="h-32 rounded-2xl" />
          ))}
        </div>
      </div>
    </main>
  );
}

/**
 * A failed request, with the recovery affordance attached.
 *
 * An error message with no way to retry leaves the provider with nothing to do
 * but reload the page - on a phone with patchy signal in a stairwell, which is
 * where these screens are actually used, that is most of the failures.
 */
export function ErrorState({
  error,
  onRetry,
  isRetrying = false,
  title = "Something went wrong",
}: {
  error: unknown;
  onRetry?: () => void;
  isRetrying?: boolean;
  title?: string;
}) {
  return (
    <Alert
      tone="error"
      title={title}
      action={
        onRetry ? (
          <Button size="sm" variant="secondary" loading={isRetrying} onClick={onRetry}>
            Retry
          </Button>
        ) : undefined
      }
    >
      {describeError(error)}
    </Alert>
  );
}

/**
 * The expected HTTP 501 answer from a provider-api surface whose backend is
 * still being built (jobs' sibling task #147, earnings' #148 - see
 * docs/PROVIDER.md).
 *
 * Styled as a deliberate "not built yet" state, NOT as a failure: nothing is
 * broken, the provider has done nothing wrong, and there is no retry that
 * would help. Treating it as an error would train providers to ignore real
 * errors on the same screens once these backends land.
 */
export function NotYetAvailable({
  title,
  description = "We're still building this. Nothing is wrong with your account — check back soon.",
  action,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <EmptyState
      icon={
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.75"
          strokeLinecap="round"
          strokeLinejoin="round"
          className="h-6 w-6"
          aria-hidden
        >
          <circle cx="12" cy="12" r="9" />
          <path d="M12 7v5l3 2" />
        </svg>
      }
      title={title}
      description={description}
      action={action}
    />
  );
}
