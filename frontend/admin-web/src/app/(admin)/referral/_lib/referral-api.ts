/**
 * Typed client for the Admin API's referral surface (tasks 167, 170, 171):
 * `ReferralsController` and `ReferralProgramConfigController`.
 *
 * The three referral screens each built their own `apiFetch` URLs inline, which
 * is how the list screen ended up requesting `?pageSize=50` with no page
 * parameter and no way to reach row 51. Every call is authenticated - these
 * endpoints are gated behind "referral.read"/"referral.write" server-side.
 */
import { API_V1, apiFetch } from "@/lib/api";
import type {
  ReferralAdminDetail,
  ReferralAdminSearchParams,
  ReferralAdminSearchResponse,
  ReferralCostReport,
  ReferralFraudReviewRequest,
  ReferralFunnelReport,
  ReferralMilestone,
  ReferralMilestoneCreateRequest,
  ReferralProgramConfig,
  ReferralProgramConfigUpdateRequest,
} from "./referral-types";

const REFERRAL_BASE = `${API_V1}/referral`;

function query(params: object): string {
  const entries = Object.entries(params as Record<string, string | number | boolean | undefined>).filter(
    ([, value]) => value !== undefined && value !== "",
  );
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString()}`;
}

export const searchReferrals = (params: ReferralAdminSearchParams) =>
  apiFetch<ReferralAdminSearchResponse>(`${REFERRAL_BASE}${query(params)}`, { authenticated: true });

/**
 * The fraud queue is its own endpoint rather than `isFraudFlagged=true` on the
 * search: it ignores every other filter server-side, so the screen must not
 * pretend the status/search controls still apply to it.
 */
export const listFraudQueue = (params: { page?: number; pageSize?: number }) =>
  apiFetch<ReferralAdminSearchResponse>(`${REFERRAL_BASE}/fraud-queue${query(params)}`, { authenticated: true });

export const getReferral = (id: string) =>
  apiFetch<ReferralAdminDetail>(`${REFERRAL_BASE}/${id}`, { authenticated: true });

/** Flag / confirm-as-fraud / dismiss-as-false-positive. All return 204. */
export const reviewReferralFraud = (
  id: string,
  action: "flag" | "approve" | "reject",
  request: ReferralFraudReviewRequest,
) =>
  apiFetch<void>(`${REFERRAL_BASE}/${id}/${action}`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const getReferralConfig = () =>
  apiFetch<ReferralProgramConfig>(`${REFERRAL_BASE}/config`, { authenticated: true });

export const updateReferralConfig = (request: ReferralProgramConfigUpdateRequest) =>
  apiFetch<ReferralProgramConfig>(`${REFERRAL_BASE}/config`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const listReferralMilestones = () =>
  apiFetch<ReferralMilestone[]>(`${REFERRAL_BASE}/config/milestones`, { authenticated: true });

export const createReferralMilestone = (request: ReferralMilestoneCreateRequest) =>
  apiFetch<ReferralMilestone>(`${REFERRAL_BASE}/config/milestones`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const setReferralMilestoneActive = (milestoneId: string, isActive: boolean) =>
  apiFetch<ReferralMilestone>(
    `${REFERRAL_BASE}/config/milestones/${milestoneId}/${isActive ? "activate" : "deactivate"}`,
    { method: "POST", authenticated: true },
  );

/** Both reports take an optional `[fromUtc, toUtc]` instant range. */
export const getReferralFunnelReport = (range: { fromUtc?: string; toUtc?: string }) =>
  apiFetch<ReferralFunnelReport>(`${REFERRAL_BASE}/reports/funnel${query(range)}`, { authenticated: true });

export const getReferralCostReport = (range: { fromUtc?: string; toUtc?: string }) =>
  apiFetch<ReferralCostReport>(`${REFERRAL_BASE}/reports/cost${query(range)}`, { authenticated: true });
