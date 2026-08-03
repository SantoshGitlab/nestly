"use client";

import type { ReactNode } from "react";
import { Alert, Button, EmptyState } from "@/components/ui";
import { describeError } from "@/lib/api";

/**
 * The two non-empty terminal states every data-backed partner screen needs.
 *
 * These are partner-web-specific rather than candidates for components/ui.tsx:
 * `NotYetAvailable` encodes this app's HTTP 501 contract with partner-api,
 * which the other two frontends have no equivalent of, and `ErrorState` is a
 * thin composition over the kit's `Alert` rather than a new primitive.
 */

/**
 * A failed request, with the recovery affordance attached.
 *
 * An error message with no way to retry leaves the partner with nothing to do
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
 * The expected HTTP 501 answer from a partner-api surface whose backend is
 * still being built (jobs' sibling task #147, earnings' #148 - see
 * docs/PARTNER.md).
 *
 * Styled as a deliberate "not built yet" state, NOT as a failure: nothing is
 * broken, the partner has done nothing wrong, and there is no retry that
 * would help. Treating it as an error would train partners to ignore real
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
