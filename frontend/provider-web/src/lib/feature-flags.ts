"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { API_V1, apiFetch } from "@/lib/api";

/**
 * Provider-facing feature flags (SRS 12.19 "Feature flags"), mirroring
 * backend/shared/Application/Settings/SettingsContracts.cs's
 * `ProviderFeatureFlagsResponse` field-for-field. Public, unauthenticated
 * `GET /api/v1/feature-flags` on provider-api - it gates navigation/UI
 * before or without a session.
 */
export interface ProviderFeatureFlags {
  ratingsPageEnabled: boolean;
  calendarViewEnabled: boolean;
  earningsLedgerEnabled: boolean;
  offersScreenEnabled: boolean;
}

/**
 * Fail-open default: every flag true. A flag hides a genuinely optional
 * feature, so a fetch failure (or the loading state, which resolves in one
 * request) must never hide something that should be visible - the opposite
 * of a fail-closed kill switch.
 */
const ALL_ENABLED: ProviderFeatureFlags = {
  ratingsPageEnabled: true,
  calendarViewEnabled: true,
  earningsLedgerEnabled: true,
  offersScreenEnabled: true,
};

/**
 * Fetch-once feature-flag lookup for hiding optional nav entries/sections.
 * Never throws and never blocks rendering on its own - callers get
 * `ALL_ENABLED` immediately and the real flags once (if) they load, same
 * shape either way so call sites never need a loading branch.
 */
export function useFeatureFlags(): ProviderFeatureFlags {
  const query = useQuery({
    queryKey: ["feature-flags"] as const,
    queryFn: () => apiFetch<ProviderFeatureFlags>(`${API_V1}/feature-flags`),
    staleTime: Infinity,
    retry: false,
  });

  return query.data ?? ALL_ENABLED;
}

/**
 * Guards a screen whose nav entry is hidden when its flag is off: a provider
 * who already has the URL open (bookmark, back button, deep link) would
 * otherwise strand on a page nothing links to any more. Redirects once the
 * flag has actually resolved to `false` - never during the fail-open
 * loading state, which starts `true` and would otherwise bounce every
 * visitor for a moment on every load.
 */
export function useRedirectIfDisabled(enabled: boolean, fallbackHref: string): void {
  const router = useRouter();
  useEffect(() => {
    if (!enabled) {
      router.replace(fallbackHref);
    }
  }, [enabled, fallbackHref, router]);
}
