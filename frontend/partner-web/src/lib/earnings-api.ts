/**
 * Typed client for the Partner API's earnings surface (`/api/v1/earnings`).
 * Every call is authenticated. See earnings-types.ts's doc comment: the
 * backend behind this surface currently answers 501 (sibling task #148 not
 * yet merged) - api.ts's `isNotImplemented` helper is how callers detect
 * that and render an empty state instead of a hard error.
 */
import { API_V1, apiFetch } from "./api";
import type { EarningLedgerEntry, EarningsSummary, PayoutDetail, PayoutSummary } from "./earnings-types";

const EARNINGS_BASE = `${API_V1}/earnings`;

export const getEarningsSummary = () =>
  apiFetch<EarningsSummary>(`${EARNINGS_BASE}/summary`, { authenticated: true });

export const listEarningsLedger = () =>
  apiFetch<EarningLedgerEntry[]>(`${EARNINGS_BASE}/ledger`, { authenticated: true });

export const listPayouts = () =>
  apiFetch<PayoutSummary[]>(`${EARNINGS_BASE}/payouts`, { authenticated: true });

export const getPayoutDetail = (payoutId: string) =>
  apiFetch<PayoutDetail>(`${EARNINGS_BASE}/payouts/${payoutId}`, { authenticated: true });
