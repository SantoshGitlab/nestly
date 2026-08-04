"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { ScreenSkeleton } from "@/components/states";
import { isAuthenticated, subscribeToAuthChanges } from "@/lib/auth";

/**
 * Client-side guard for the authenticated partner shell.
 *
 * Mirrors admin-web/src/components/RequireAdminAuth.tsx. This is a
 * usability measure, not a security boundary: the token lives in the
 * browser, so anything rendered here is reachable by a determined user. The
 * actual enforcement is the [Authorize] attribute on the Partner API - every
 * request these screens make is rejected server-side without a valid JWT,
 * and lib/api.ts's apiFetch clears the local session the moment the server
 * says a token is no longer good (401), which this guard reacts to
 * immediately via subscribeToAuthChanges - covering both "never logged in"
 * and "was logged in, token just expired/was revoked" in the same code path.
 */
export function RequirePartnerAuth({ children }: { children: ReactNode }) {
  const router = useRouter();
  // Undefined until the first client render: sessionStorage does not exist
  // during SSR, and guessing either way would flash the wrong UI.
  const [authed, setAuthed] = useState<boolean | undefined>(undefined);

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

  // A bare "Loading…" line here reflowed the whole shell the moment the
  // session resolved, on every authenticated route in the app.
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
