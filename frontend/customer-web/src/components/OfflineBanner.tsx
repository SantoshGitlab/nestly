"use client";

import { useEffect, useState } from "react";
import { Alert, IconButton } from "@/components/ui";

/**
 * Explicit offline state (task #355). Before this, a customer who lost
 * connectivity got whatever each individual data-fetching hook happened to
 * render for its own failed request — a generic "something went wrong" on
 * one screen, a silently-stale list on another — with nothing telling them
 * the actual, single cause. This is a `navigator.onLine` + `online`/`offline`
 * listener mounted once near the app root, not a retry/queue/offline-cache
 * architecture: it only ever answers one question ("is the browser currently
 * reporting a connection?") and lets every screen keep failing however it
 * already does.
 *
 * `navigator.onLine` is a browser-reported signal, not a real reachability
 * check (a captive portal or a dead upstream link both report `true`), so
 * this deliberately undersells itself as "You're offline" rather than
 * "You have no internet" — it can go wrong in the optimistic direction, never
 * the alarming one.
 */
export function OfflineBanner() {
  // Starts `false` rather than reading `navigator.onLine` at first render:
  // this mounts in the server-rendered root layout, where `navigator` doesn't
  // exist, and seeding from it would desync server/client markup. The effect
  // below corrects this on mount, before the customer can act on anything.
  const [offline, setOffline] = useState(false);
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    setOffline(!navigator.onLine);

    const onOffline = () => {
      setOffline(true);
      setDismissed(false); // a fresh drop deserves a fresh banner, even if the last one was dismissed
    };
    const onOnline = () => setOffline(false);

    window.addEventListener("offline", onOffline);
    window.addEventListener("online", onOnline);
    return () => {
      window.removeEventListener("offline", onOffline);
      window.removeEventListener("online", onOnline);
    };
  }, []);

  if (!offline || dismissed) return null;

  return (
    <div className="fixed inset-x-0 top-[4.5rem] z-30 px-4 pt-2 sm:px-6">
      <div className="mx-auto w-full max-w-7xl">
        <Alert
          tone="warning"
          action={
            <IconButton label="Dismiss" onClick={() => setDismissed(true)}>
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                className="h-4 w-4"
                aria-hidden
              >
                <path d="M18 6 6 18M6 6l12 12" />
              </svg>
            </IconButton>
          }
        >
          You&apos;re offline — some pages may not load until your connection comes back.
        </Alert>
      </div>
    </div>
  );
}
