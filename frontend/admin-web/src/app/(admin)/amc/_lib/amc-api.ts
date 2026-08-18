import { API_V1, apiFetch } from "@/lib/api";

/**
 * Typed client for the admin AMC surface (docs/AMC.md, Phase 20): plan
 * catalog CRUD, contract search/detail, and the renewal-pipeline report.
 *
 * Routes verified directly against the landed
 * `AmcPlansController`/`AmcContractsController` (admin-api) - both are flat
 * (`admin/amc-plans`, `admin/amc-contracts`), mirroring
 * `SubscriptionPlansController`'s (`admin/subscription-plans`) and
 * `RecurringPlansController`'s (`admin/recurring-plans`) own flat naming,
 * not the nested `admin/<group>/<resource>` shape catalog/cms/referral use.
 *
 * Lives under `amc/_lib` rather than `src/lib` for the same reason
 * `subscription-plans/_lib/plans-api.ts` and `bookings/_lib/recurring-plans-api.ts`
 * do - nothing outside this module consumes it.
 */

/**
 * Mirrors Nestly.Domain.CustomerAmcContractStatus's declaration order
 * exactly. Neither admin-api nor consumer-api registers a
 * JsonStringEnumConverter (verified against AmcContractsController.cs
 * directly), so this crosses the wire as its ordinal - same convention as
 * `RecurringPlanStatus` in `recurring-plans-api.ts`.
 */
export enum CustomerAmcContractStatus {
  Active = 0,
  Exhausted = 1,
  Expired = 2,
  Cancelled = 3,
}

export const CONTRACT_STATUS_LABELS: Record<CustomerAmcContractStatus, string> = {
  [CustomerAmcContractStatus.Active]: "Active",
  [CustomerAmcContractStatus.Exhausted]: "Exhausted",
  [CustomerAmcContractStatus.Expired]: "Expired",
  [CustomerAmcContractStatus.Cancelled]: "Cancelled",
};

export interface AmcPlanAdminResponse {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  description: string | null;
  price: number;
  termMonths: number;
  visitsIncluded: number;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** Same shape for create and update - mirrors AmcPlanCreateRequest/AmcPlanUpdateRequest, which are identical. */
export interface AmcPlanRequest {
  categoryId: string;
  name: string;
  description: string | null;
  price: number;
  termMonths: number;
  visitsIncluded: number;
}

export interface AmcContractAdminListItemResponse {
  id: string;
  customerId: string;
  customerName: string;
  planName: string;
  assetLabel: string;
  status: CustomerAmcContractStatus;
  startDateUtc: string;
  endDateUtc: string;
  visitsIncluded: number;
  visitsRemaining: number;
  createdAtUtc: string;
}

export interface AmcContractAdminSearchResponse {
  items: AmcContractAdminListItemResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AmcContractStatusCount {
  status: CustomerAmcContractStatus;
  contractCount: number;
}

export interface AmcRenewalReportResponse {
  totalContracts: number;
  byStatus: AmcContractStatusCount[];
  horizonFromUtc: string;
  horizonToUtc: string;
  expiringInHorizon: number;
  exhaustedInHorizon: number;
  expiringOrExhaustedContracts: AmcContractAdminListItemResponse[];
}

export interface AmcContractSearchParams {
  status?: string;
  customerSearch?: string;
  page: number;
  pageSize: number;
}

function query(params: object): string {
  const entries = Object.entries(params as Record<string, string | number | boolean | undefined>).filter(
    ([, value]) => value !== undefined && value !== "",
  );
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString()}`;
}

const PLANS_BASE = `${API_V1}/amc-plans`;
const CONTRACTS_BASE = `${API_V1}/amc-contracts`;

// ---- Plan catalog ----

export const listAmcPlans = () => apiFetch<AmcPlanAdminResponse[]>(PLANS_BASE, { authenticated: true });

export const createAmcPlan = (request: AmcPlanRequest) =>
  apiFetch<AmcPlanAdminResponse>(PLANS_BASE, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateAmcPlan = (id: string, request: AmcPlanRequest) =>
  apiFetch<AmcPlanAdminResponse>(`${PLANS_BASE}/${id}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const setAmcPlanActive = (id: string, isActive: boolean) =>
  apiFetch<void>(`${PLANS_BASE}/${id}/${isActive ? "activate" : "deactivate"}`, {
    method: "POST",
    authenticated: true,
  });

// ---- Contracts ----

export function searchAmcContracts(
  params: AmcContractSearchParams,
): Promise<AmcContractAdminSearchResponse> {
  return apiFetch<AmcContractAdminSearchResponse>(`${CONTRACTS_BASE}${query(params)}`, {
    authenticated: true,
  });
}

export const getAmcContract = (id: string) =>
  apiFetch<AmcContractAdminListItemResponse>(`${CONTRACTS_BASE}/${id}`, { authenticated: true });

/**
 * Both bounds are full UTC instants (unlike `recurring-plans-api.ts`'s
 * `DateOnly`-typed report horizon) - `GetRenewalReportAsync` takes
 * `DateTime?`, matching the referral reports' own `fromUtc`/`toUtc` shape.
 * Omitting both asks the server for its default 30-day horizon.
 */
export function getAmcRenewalReport(range: {
  fromUtc?: string;
  toUtc?: string;
}): Promise<AmcRenewalReportResponse> {
  return apiFetch<AmcRenewalReportResponse>(`${CONTRACTS_BASE}/renewal-report${query(range)}`, {
    authenticated: true,
  });
}
