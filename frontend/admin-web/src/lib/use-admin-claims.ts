"use client";

import { useEffect, useState } from "react";
import { getSessionClaims, subscribeToAuthChanges } from "./auth";
import type { AdminSessionClaims } from "./types";

/**
 * The signed-in admin's token claims, kept in sync with the auth store.
 *
 * This exact hook was copy-pasted into a dozen module screens; it is here now
 * so a change to how claims are sourced happens once. Claims start `null` and
 * arrive after mount deliberately — reading localStorage during render would
 * make the server and client markup disagree.
 *
 * These claims drive what the UI *offers*, never what is *allowed*: every
 * admin route is still authorised server-side.
 */
export function useAdminClaims(): AdminSessionClaims | null {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  return claims;
}
