"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { ScreenSkeleton } from "@/components/patterns";
import { isAuthenticated, subscribeToAuthChanges } from "@/lib/auth";

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
