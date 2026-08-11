"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { ScreenSkeleton } from "@/components/patterns";
import { isAuthenticated, subscribeToAuthChanges } from "@/lib/auth";
import { buildLoginHref } from "@/lib/return-to";

/**
 * Client-side guard for the signed-in screens.
 *
 * This is a usability measure, not a security boundary: the token lives in the
 * browser, so anything rendered here is reachable by a determined user. The
 * actual enforcement is the [Authorize] attribute on the API — every request
 * these screens make is rejected server-side without a valid JWT.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const router = useRouter();
  // Undefined only for the SSR/first-paint render: sessionStorage does not
  // exist there, and guessing either way would flash the wrong UI. Every
  // client-side navigation after that mounts this component with `window`
  // already defined, so it can read the real value immediately instead of
  // forcing an extra skeleton-flash frame through a useEffect first - this
  // guard is remounted on every one of the ~20 authenticated routes below,
  // since none of them share a layout that would keep a single instance.
  const [authed, setAuthed] = useState<boolean | undefined>(() =>
    typeof window === "undefined" ? undefined : isAuthenticated(),
  );

  useEffect(() => {
    const sync = () => setAuthed(isAuthenticated());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  useEffect(() => {
    if (authed !== false) return;

    // The destination travels with the redirect so signing in returns the
    // customer to what they were doing (see lib/return-to.ts). Still
    // `replace` rather than `push`: leaving the guarded URL in history means
    // Back from the login page bounces straight off this guard again.
    //
    // Read from `window` rather than usePathname/useSearchParams: this only
    // runs client-side at redirect time, and useSearchParams would opt every
    // screen behind this guard out of static rendering (it needs a Suspense
    // boundary in each of them, which the production build enforces).
    router.replace(buildLoginHref(window.location.pathname, window.location.search));
  }, [authed, router]);

  // Every screen behind this guard opens with a heading plus stacked cards, so
  // the shared page skeleton is the shape that actually gets replaced. A bare
  // "Loading…" line here reflowed the whole page the moment the session
  // resolved, on every authenticated route in the app.
  if (authed === undefined) {
    return <ScreenSkeleton />;
  }

  if (!authed) {
    return (
      <ScreenSkeleton>
        <p role="status" className="text-sm text-fg-muted">
          Redirecting you to sign in…
        </p>
      </ScreenSkeleton>
    );
  }

  return <>{children}</>;
}
