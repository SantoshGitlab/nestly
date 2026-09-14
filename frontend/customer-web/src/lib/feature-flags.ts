"use client";

import { useQuery } from "@tanstack/react-query";
import { API_V1, apiFetch } from "@/lib/api";

/**
 * Customer-facing feature flags (SRS 12.19 "Feature flags"), mirroring
 * backend/shared/Application/Settings/SettingsContracts.cs's
 * `CustomerFeatureFlagsResponse` field-for-field. Public, unauthenticated
 * `GET /api/v1/feature-flags` on consumer-api - it gates navigation/UI
 * before or without a session.
 */
export interface CustomerFeatureFlags {
  walletEnabled: boolean;
  couponsEnabled: boolean;
  referralsEnabled: boolean;
  amcSubscriptionsEnabled: boolean;
  serviceRatingsEnabled: boolean;
  bookingHelpLinkEnabled: boolean;
}

/**
 * Fail-open default: every flag true. A flag hides a genuinely optional
 * feature, so a fetch failure (or the loading state, which resolves in one
 * request) must never hide something that should be visible - the opposite
 * of a fail-closed kill switch.
 */
const ALL_ENABLED: CustomerFeatureFlags = {
  walletEnabled: true,
  couponsEnabled: true,
  referralsEnabled: true,
  amcSubscriptionsEnabled: true,
  serviceRatingsEnabled: true,
  bookingHelpLinkEnabled: true,
};

/**
 * Fetch-once feature-flag lookup for hiding optional nav entries/sections.
 * Never throws and never blocks rendering on its own - callers get
 * `ALL_ENABLED` immediately and the real flags once (if) they load, same
 * shape either way so call sites never need a loading branch.
 */
export function useFeatureFlags(): CustomerFeatureFlags {
  const query = useQuery({
    queryKey: ["feature-flags"] as const,
    queryFn: () => apiFetch<CustomerFeatureFlags>(`${API_V1}/feature-flags`),
    staleTime: Infinity,
    retry: false,
  });

  return query.data ?? ALL_ENABLED;
}
