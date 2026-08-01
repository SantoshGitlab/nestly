/**
 * Response shapes for the Partner API's earnings surface
 * (`/api/v1/earnings`, docs/PARTNER.md's Financial domain:
 * `partner_earning_ledger` / `partner_payout`).
 *
 * NOTE ON PROVENANCE: same caveat as jobs-types.ts - the backend behind this
 * surface is currently a stub returning HTTP 501 pending sibling task #148.
 * Only the fields the task brief committed to are required; everything else
 * is read defensively via `[key: string]: unknown` so this client survives
 * the real shape landing without a rewrite.
 */

export interface EarningsSummary {
  totalEarned: number;
  pendingPayout: number;
  [key: string]: unknown;
}

/** One append-only entry in the partner's earning ledger (credit per completed job, debit for penalties). */
export interface EarningLedgerEntry {
  id: string;
  amount: number;
  description?: string;
  createdAtUtc?: string;
  [key: string]: unknown;
}

export interface PayoutSummary {
  id: string;
  status?: string;
  totalAmount?: number;
  periodStart?: string;
  periodEnd?: string;
  [key: string]: unknown;
}

export interface PayoutDetail extends PayoutSummary {
  payoutReference?: string | null;
}
