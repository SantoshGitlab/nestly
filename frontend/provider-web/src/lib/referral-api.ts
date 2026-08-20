/**
 * Typed client for the Provider API's referral surface (`/api/v1/referral`),
 * mirrors lib/earnings-api.ts. Every call is authenticated and scoped to the
 * caller's own provider id.
 */
import { API_V1, apiFetch } from "./api";
import type { ProviderReferralHistoryItem, ProviderReferralSummary } from "./referral-types";

const REFERRAL_BASE = `${API_V1}/referral`;

export const getReferralSummary = () =>
  apiFetch<ProviderReferralSummary>(REFERRAL_BASE, { authenticated: true });

export const getReferralHistory = () =>
  apiFetch<ProviderReferralHistoryItem[]>(`${REFERRAL_BASE}/history`, { authenticated: true });
