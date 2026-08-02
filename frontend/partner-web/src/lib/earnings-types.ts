/**
 * Response shapes for the Partner API's earnings surface (`/api/v1/earnings`).
 * Mirrors backend/shared/Application/PartnerManagement/PartnerFinancialContracts.cs
 * (PartnerEarningsSummaryResponse / PartnerEarningLedgerEntryResponse /
 * PartnerPayoutSearchResponse / PartnerPayoutResponse) field-for-field,
 * camelCased per ASP.NET Core's default JSON naming policy, with enums as
 * numeric ordinals (no JsonStringEnumConverter registered - same convention
 * as jobs-types.ts's JobStatus) - keep this in sync if that file changes.
 */

export enum EarningEntryType {
  Credit = 0,
  Debit = 1,
}

export enum EarningSourceType {
  JobCompletion = 0,
  Penalty = 1,
  AdminCorrection = 2,
}

/** One append-only entry in the partner's earning ledger. */
export interface EarningLedgerEntry {
  id: string;
  partnerId: string;
  entryType: EarningEntryType;
  amount: number;
  balanceAfter: number;
  sourceType: EarningSourceType;
  sourceReferenceId: string | null;
  description: string;
  createdAtUtc: string;
}

/** GET /earnings/summary's response (PartnerEarningsSummaryResponse). */
export interface EarningsSummary {
  partnerId: string;
  currentBalance: number;
  entries: EarningLedgerEntry[];
}

export enum PayoutStatus {
  Pending = 0,
  Processing = 1,
  Paid = 2,
  Failed = 3,
}

export const PAYOUT_STATUS_LABELS: Record<PayoutStatus, string> = {
  [PayoutStatus.Pending]: "Pending",
  [PayoutStatus.Processing]: "Processing",
  [PayoutStatus.Paid]: "Paid",
  [PayoutStatus.Failed]: "Failed",
};

export function payoutStatusLabel(status: PayoutStatus): string {
  return PAYOUT_STATUS_LABELS[status] ?? String(status);
}

/** One row in the partner's payout list (PartnerPayoutResponse). */
export interface PayoutSummary {
  id: string;
  partnerId: string;
  partnerDisplayName: string;
  periodStart: string;
  periodEnd: string;
  totalAmount: number;
  status: PayoutStatus;
  payoutReference: string | null;
  notes: string | null;
  createdAt: string;
}

/** GET /earnings/payouts's actual response envelope (PartnerPayoutSearchResponse). */
export interface PayoutListResponse {
  items: PayoutSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** GET /earnings/payouts/{id}'s response - same shape as a list row. */
export type PayoutDetail = PayoutSummary;
