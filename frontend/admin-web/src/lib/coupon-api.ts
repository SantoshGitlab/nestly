/**
 * Typed client for the Admin API's coupon surface (SRS 12.12, task 118/119):
 * `CouponsController` - coupon CRUD with every rule dimension plus
 * redemption reporting. Every call is authenticated - these are admin-only
 * endpoints gated behind the "coupons" permission module server-side.
 */
import { API_V1, apiFetch } from "./api";
import type {
  CategoryLookupResponse,
  CouponAdminResponse,
  CouponAdminSearchResponse,
  CouponCreateRequest,
  CouponRedemptionReportParams,
  CouponRedemptionReportResponse,
  CouponSearchParams,
  CouponUpdateRequest,
} from "./coupon-types";

const COUPONS_BASE = `${API_V1}/coupons`;

// Parameter typed as `object` (not `Record<string, ...>`) so that named
// interfaces like CouponSearchParams - which have no index signature of
// their own - can be passed in without a cast; the entries are read via a
// loosely-typed view of the same object instead.
function query(params: object): string {
  const entries = Object.entries(params as Record<string, string | number | boolean | undefined>)
    .filter(([, value]) => value !== undefined);
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString()}`;
}

export const listApplicableCategories = () =>
  apiFetch<CategoryLookupResponse[]>(`${COUPONS_BASE}/categories`, { authenticated: true });

export const searchCoupons = (params: CouponSearchParams) =>
  apiFetch<CouponAdminSearchResponse>(`${COUPONS_BASE}${query(params)}`, { authenticated: true });

export const getCoupon = (id: string) =>
  apiFetch<CouponAdminResponse>(`${COUPONS_BASE}/${id}`, { authenticated: true });

export const createCoupon = (request: CouponCreateRequest) =>
  apiFetch<CouponAdminResponse>(COUPONS_BASE, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateCoupon = (id: string, request: CouponUpdateRequest) =>
  apiFetch<CouponAdminResponse>(`${COUPONS_BASE}/${id}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const setCouponActive = (id: string, isActive: boolean) =>
  apiFetch<void>(`${COUPONS_BASE}/${id}/${isActive ? "activate" : "deactivate"}`, {
    method: "POST",
    authenticated: true,
  });

export const getCouponRedemptionReport = (params: CouponRedemptionReportParams) =>
  apiFetch<CouponRedemptionReportResponse>(`${COUPONS_BASE}/redemptions/report${query(params)}`, {
    authenticated: true,
  });
