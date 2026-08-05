"use client";

import Link from "next/link";
import { useEffect } from "react";
import { Button } from "@/components/ui";

/**
 * Route-level error boundary (task 228). Without this file an uncaught render
 * error anywhere below the root layout falls through to Next's built-in
 * screen, which in a production build is an unstyled "Application error: a
 * client-side exception has occurred" with no way back into the app.
 *
 * `digest` is the only part of the error safe to put on screen: Next replaces
 * server error messages with this hash precisely so the real message stays on
 * the server, and it is what support needs to find the matching log line.
 */
export default function ProviderError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    // Surfaced to the browser console so it is still recoverable in dev and in
    // a user's own devtools; wiring this to a real reporter is a later task.
    console.error("Unhandled provider-web error:", error);
  }, [error]);

  return (
    <main className="flex min-h-screen items-center justify-center px-4 py-12">
      <div className="w-full max-w-md text-center">
        <span
          aria-hidden
          className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-danger-soft text-danger"
        >
          <svg viewBox="0 0 24 24" fill="none" className="h-6 w-6">
            <path
              d="M12 9v4m0 4h.01M10.3 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.7 3.86a2 2 0 0 0-3.4 0Z"
              stroke="currentColor"
              strokeWidth="1.75"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </span>

        <h1 className="mt-6 text-display-sm font-semibold text-fg">Something went wrong</h1>
        <p className="mt-2 text-sm leading-relaxed text-fg-muted text-pretty">
          This screen failed to load. Trying again usually fixes it — your account and jobs are
          unaffected.
        </p>

        {error.digest ? (
          <p className="nums mt-4 text-xs text-fg-subtle">
            Reference: <span className="font-medium text-fg-muted">{error.digest}</span>
          </p>
        ) : null}

        <div className="mt-8 flex flex-col gap-2 sm:flex-row sm:justify-center">
          <Button type="button" onClick={reset}>
            Try again
          </Button>
          <Link
            href="/dashboard"
            className="inline-flex h-10 items-center justify-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-fg shadow-xs transition duration-fast ease-out hover:border-line-strong hover:bg-surface-2"
          >
            Go to dashboard
          </Link>
        </div>
      </div>
    </main>
  );
}
