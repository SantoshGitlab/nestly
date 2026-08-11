"use client";

import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { ProviderHeader } from "@/components/ProviderHeader";
import { ProviderSidebar, ProviderTabBar } from "@/components/ProviderSidebar";
import { RequireProviderAuth } from "@/components/RequireProviderAuth";
import { Alert } from "@/components/ui";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { DevicePlatform, registerDeviceToken, storeDeviceTokenId } from "@/lib/device-tokens-api";
import { getProfile } from "@/lib/profile-api";
import { requestPushToken } from "@/lib/push";
import type { ProviderSessionClaims } from "@/lib/types";

/**
 * Authenticated app shell: header + navigation + content area, shown once
 * signed in. Every route nested under the `(provider)` route group (this
 * segment does not appear in the URL) automatically gets this chrome and
 * the RequireProviderAuth guard. Mirrors admin-web's `(admin)/layout.tsx`.
 *
 * Navigation is a side rail from `md` up and a bottom tab bar below it -
 * providers work from a phone in the field, so the four sections stay one
 * thumb-tap away rather than behind a drawer.
 */
export default function AuthenticatedLayout({ children }: { children: ReactNode }) {
  const [claims, setClaims] = useState<ProviderSessionClaims | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  // Task 319: a PendingVerification provider can open every screen under
  // this shell (login/routing allow it deliberately - see
  // ProviderLoginService, which only refuses Suspended/Deactivated), but
  // jobs/earnings can never populate until an admin approves their KYC, and
  // nothing told them why. One profile fetch here surfaces that everywhere
  // rather than duplicating the check on each screen.
  const profileQuery = useQuery({ queryKey: ["provider-profile"], queryFn: getProfile });

  // Fires once per mount of the authenticated shell (i.e. once per sign-in,
  // since this layout unmounts on sign-out) - job offers are time-sensitive
  // (task 307), so push registration happens as soon as the provider is in
  // rather than waiting for them to visit a specific screen. No-ops entirely
  // when Firebase is not configured or the browser declines - see
  // lib/push.ts.
  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const token = await requestPushToken();
      if (cancelled || !token) return;

      try {
        const registered = await registerDeviceToken(DevicePlatform.Fcm, token);
        if (!cancelled) storeDeviceTokenId(registered.id);
      } catch {
        // Best-effort: a provider with no working push is still a working
        // provider. Nothing here blocks any other part of the app.
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <RequireProviderAuth>
      <div className="flex min-h-screen flex-col bg-bg">
        <ProviderHeader claims={claims} />

        <div className="flex flex-1">
          <ProviderSidebar />

          {/* Bottom padding clears the fixed tab bar so the last element on a
              page is never trapped underneath it. */}
          <main className="min-w-0 flex-1 px-4 py-6 pb-24 sm:px-6 md:pb-6 lg:px-8">
            <div className="mx-auto w-full max-w-5xl">
              {profileQuery.data?.status === "PendingVerification" ? (
                <Alert tone="warning" title="Your account is pending verification" >
                  Jobs and earnings will start appearing once an admin approves your KYC
                  documents. Finish submitting them from your{" "}
                  <a href="/profile" className="font-medium underline underline-offset-2">
                    profile
                  </a>{" "}
                  if you haven&apos;t already — approval is usually the only thing standing
                  between you and your first job.
                </Alert>
              ) : null}
              <div className={profileQuery.data?.status === "PendingVerification" ? "mt-4" : undefined}>
                {children}
              </div>
            </div>
          </main>
        </div>

        <ProviderTabBar />
      </div>
    </RequireProviderAuth>
  );
}
