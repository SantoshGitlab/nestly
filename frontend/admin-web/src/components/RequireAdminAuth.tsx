"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Spinner } from "@/components/ui";
import { isAuthenticated, subscribeToAuthChanges } from "@/lib/auth";

/**
 * Client-side guard for the authenticated admin shell (task 98d, SRS 25.2).
 *
 * Mirrors customer-web/src/components/RequireAuth.tsx. This is a usability
 * measure, not a security boundary: the token lives in the browser, so
 * anything rendered here is reachable by a determined user. The actual
 * enforcement is the [Authorize] attribute on the Admin API - every request
 * these screens make is rejected server-side without a valid JWT, and
 * lib/api.ts's apiFetch clears the local session the moment the server says
 * a token is no longer good (401), which this guard reacts to immediately
 * via subscribeToAuthChanges - covering both "never logged in" and
 * "was logged in, token just expired/was revoked" in the same code path.
 */
export function RequireAdminAuth({ children }: { children: ReactNode }) {
  const router = useRouter();
  // Undefined only for the SSR/first-paint render: sessionStorage does not
  // exist there, and guessing either way would flash the wrong UI. Every
  // later client-side render has `window` defined, so read the real value
  // immediately instead of forcing an extra loading frame through a
  // useEffect first.
  const [authed, setAuthed] = useState<boolean | undefined>(() =>
    typeof window === "undefined" ? undefined : isAuthenticated(),
  );

  useEffect(() => {
    const sync = () => setAuthed(isAuthenticated());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  useEffect(() => {
    if (authed === false) {
      router.replace("/login");
    }
  }, [authed, router]);

  if (authed === undefined || !authed) {
    // aria-live so a screen reader hears the transition rather than landing on
    // a silently-changed page; polite because neither state is an alert.
    return (
      <p
        aria-live="polite"
        className="flex items-center gap-2 p-8 text-sm text-fg-muted"
      >
        <Spinner />
        {authed === undefined ? "Loading…" : "Redirecting to sign in…"}
      </p>
    );
  }

  return <>{children}</>;
}
