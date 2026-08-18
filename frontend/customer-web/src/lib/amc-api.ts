/**
 * Typed client for the customer-facing AMC surface (docs/AMC.md, Phase 20):
 * browse plans, purchase a contract, list/view "my contracts", cancel, and
 * redeem entitlement into an ordinary booking.
 *
 * A dedicated module rather than inline `apiFetch` calls (the pattern
 * `app/subscription/page.tsx` uses): Subscription is a single page, so
 * inlining cost nothing. AMC spans four routes (browse/purchase, "my
 * contracts" list, contract detail, redeem) that all need the same five
 * endpoints - factoring them out once avoids re-typing the same fetch calls
 * on every page, the same reasoning admin-web's `_lib/*-api.ts` modules are
 * built on.
 *
 * Two of these paths are a best-effort reading of docs/AMC.md's API SURFACE
 * section, not yet verified against a live backend:
 * - `POST /me/amc-contracts/{id}/cancel` - AMC.md does not name a cancel
 *   endpoint at all; this mirrors SubscriptionController's own
 *   `POST /subscription/{id}/cancel` (the nearest precedent: same "cancel a
 *   prepaid commitment" shape) rather than a bare DELETE, since every
 *   existing cancel-style endpoint in this codebase is POST .../cancel, not
 *   DELETE.
 * - The redeem request/response shapes assume `BookingSummaryRequestBody`
 *   in, `{ id: string }` out (same minimal shape `booking/summary/page.tsx`
 *   reads off `POST /bookings`) - docs/AMC.md says redeem takes "the same
 *   request shape as a normal booking, minus payment" and returns a
 *   `BookingDetailResponse`, of which only `id` is used here today.
 *
 * Both should be re-verified once AmcController lands in consumer-api.
 */
import { API_V1, apiFetch } from "./api";
import type {
  AmcContractPurchaseRequestBody,
  AmcPlanBrowseResponse,
  BookingSummaryRequestBody,
  MyAmcContractResponse,
} from "./types";

const AMC_BASE = `${API_V1}/amc`;
const MY_CONTRACTS_BASE = `${API_V1}/me/amc-contracts`;

export const browseAmcPlans = (categoryId?: string) =>
  apiFetch<AmcPlanBrowseResponse[]>(
    `${AMC_BASE}/plans${categoryId ? `?categoryId=${categoryId}` : ""}`,
    { authenticated: true },
  );

export const purchaseAmcContract = (request: AmcContractPurchaseRequestBody) =>
  apiFetch<MyAmcContractResponse>(`${AMC_BASE}/contracts`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const listMyAmcContracts = () =>
  apiFetch<MyAmcContractResponse[]>(MY_CONTRACTS_BASE, { authenticated: true });

export const getMyAmcContract = (contractId: string) =>
  apiFetch<MyAmcContractResponse>(`${MY_CONTRACTS_BASE}/${contractId}`, { authenticated: true });

export const cancelMyAmcContract = (contractId: string) =>
  apiFetch<void>(`${MY_CONTRACTS_BASE}/${contractId}/cancel`, {
    method: "POST",
    authenticated: true,
  });

/**
 * Redeems one visit against a contract: creates a zero-priced booking
 * through the same address/slot/service selection a normal booking uses
 * (docs/AMC.md), linked back to the contract. Only `id` off the response is
 * read today - the redeem flow hands off to the existing booking detail page
 * (`/bookings/[id]`) rather than duplicating `BookingDetailResponse` here.
 */
export const redeemAmcContractVisit = (contractId: string, request: BookingSummaryRequestBody) =>
  apiFetch<{ id: string }>(`${MY_CONTRACTS_BASE}/${contractId}/redeem`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });
