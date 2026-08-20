/**
 * Typed client for the Admin API's provider-referral surface
 * (PROVIDER-REFERRAL.md): `ProviderReferralsController` and
 * `ProviderReferralProgramConfigController`. Mirrors
 * `(admin)/referral/_lib/referral-api.ts`.
 */
import { API_V1, apiFetch } from "@/lib/api";
import type {
  ProviderReferralAdminDetail,
  ProviderReferralAdminSearchParams,
  ProviderReferralAdminSearchResponse,
  ProviderReferralFraudReviewRequest,
  ProviderReferralProgramConfig,
  ProviderReferralProgramConfigUpdateRequest,
} from "./provider-referral-types";

const PROVIDER_REFERRAL_BASE = `${API_V1}/provider-referral`;

function query(params: object): string {
  const entries = Object.entries(params as Record<string, string | number | boolean | undefined>).filter(
    ([, value]) => value !== undefined && value !== "",
  );
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString()}`;
}

export const searchProviderReferrals = (params: ProviderReferralAdminSearchParams) =>
  apiFetch<ProviderReferralAdminSearchResponse>(`${PROVIDER_REFERRAL_BASE}${query(params)}`, {
    authenticated: true,
  });

/** The fraud queue is its own endpoint rather than `isFraudFlagged=true` on the search - it ignores every other filter server-side. */
export const listProviderReferralFraudQueue = (params: { page?: number; pageSize?: number }) =>
  apiFetch<ProviderReferralAdminSearchResponse>(`${PROVIDER_REFERRAL_BASE}/fraud-queue${query(params)}`, {
    authenticated: true,
  });

export const getProviderReferral = (id: string) =>
  apiFetch<ProviderReferralAdminDetail>(`${PROVIDER_REFERRAL_BASE}/${id}`, { authenticated: true });

/** Flag / confirm-as-fraud / dismiss-as-false-positive. All return 204. */
export const reviewProviderReferralFraud = (
  id: string,
  action: "flag" | "approve" | "reject",
  request: ProviderReferralFraudReviewRequest,
) =>
  apiFetch<void>(`${PROVIDER_REFERRAL_BASE}/${id}/${action}`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const getProviderReferralConfig = () =>
  apiFetch<ProviderReferralProgramConfig>(`${PROVIDER_REFERRAL_BASE}/config`, { authenticated: true });

export const updateProviderReferralConfig = (request: ProviderReferralProgramConfigUpdateRequest) =>
  apiFetch<ProviderReferralProgramConfig>(`${PROVIDER_REFERRAL_BASE}/config`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });
