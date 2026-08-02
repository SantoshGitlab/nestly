"use client";

import { useQuery } from "@tanstack/react-query";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { WalletEntryType, WalletSourceType } from "@/lib/types";
import type { WalletBalanceResponse, WalletLedgerEntryResponse } from "@/lib/types";

/**
 * Readable label for a ledger entry's source (SRS 11.17, GUIDELINES #4 of
 * docs/NESTLY-COINS.md - "a breakdown showing how much of the applied
 * balance was coins vs. other credit"). Real gap fixed here, not just
 * NestlyCoinsReward added alongside it (task 203): the three Referral
 * source types already existed on the backend but were never added to this
 * switch, silently falling through to the generic "Wallet" label.
 */
function sourceLabel(sourceType: WalletSourceType): string {
  switch (sourceType) {
    case WalletSourceType.Refund:
      return "Refund";
    case WalletSourceType.PromotionalCredit:
      return "Promotional credit";
    case WalletSourceType.ManualAdjustment:
      return "Adjustment";
    case WalletSourceType.ReferralReward:
      return "Referral reward";
    case WalletSourceType.ReferralMilestoneBonus:
      return "Referral milestone bonus";
    case WalletSourceType.ReferralCreditExpiry:
      return "Referral credit expired";
    case WalletSourceType.NestlyCoinsReward:
      return "Nestly Coins earned";
    case WalletSourceType.NestlyCoinsClawback:
      return "Nestly Coins clawed back";
    default:
      return "Wallet";
  }
}

/**
 * Wallet balance and ledger (tasks 78a-b, SRS 11.17).
 */
export default function WalletPage() {
  return (
    <RequireAuth>
      <WalletScreen />
    </RequireAuth>
  );
}

function WalletScreen() {
  const balanceQuery = useQuery({
    queryKey: ["wallet-balance"],
    queryFn: () => apiFetch<WalletBalanceResponse>(`${API_V1}/wallet/balance`, { authenticated: true }),
  });

  const ledgerQuery = useQuery({
    queryKey: ["wallet-ledger"],
    queryFn: () =>
      apiFetch<WalletLedgerEntryResponse[]>(`${API_V1}/wallet/ledger`, { authenticated: true }),
  });

  return (
    <main className="mx-auto w-full max-w-3xl px-6 py-12">
      <PageHeading title="Wallet" subtitle="Your Nestly wallet balance and transaction history." />

      <div className="mb-8">
        {balanceQuery.isPending ? (
          <Card title="Balance">
            <p className="text-sm text-neutral-500">Loading…</p>
          </Card>
        ) : balanceQuery.isError ? (
          <Card title="Balance">
            <Alert>{describeError(balanceQuery.error)}</Alert>
          </Card>
        ) : (
          <Card title="Balance">
            <p className="text-3xl font-semibold">₹{balanceQuery.data.balance.toFixed(2)}</p>
          </Card>
        )}
      </div>

      <h2 className="mb-3 text-lg font-semibold">Transaction history</h2>

      {ledgerQuery.isPending ? (
        <p className="text-sm text-neutral-500">Loading transactions…</p>
      ) : ledgerQuery.isError ? (
        <Alert>{describeError(ledgerQuery.error)}</Alert>
      ) : ledgerQuery.data.length === 0 ? (
        <Card title="No transactions yet">
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            Wallet credits and debits will show up here once you have some.
          </p>
        </Card>
      ) : (
        <ul className="flex flex-col gap-3">
          {ledgerQuery.data.map((entry) => {
            const isCredit = entry.entryType === WalletEntryType.Credit;
            return (
              <li key={entry.id}>
                <Card title={sourceLabel(entry.sourceType)}>
                  <div className="flex items-center justify-between gap-3 text-sm">
                    <div className="flex flex-col gap-1 text-neutral-600 dark:text-neutral-400">
                      <span>{entry.description}</span>
                      <span>{new Date(entry.createdAtUtc).toLocaleString()}</span>
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <span
                        className={`font-semibold ${
                          isCredit
                            ? "text-green-700 dark:text-green-400"
                            : "text-red-700 dark:text-red-400"
                        }`}
                      >
                        {isCredit ? "+" : "-"}₹{entry.amount.toFixed(2)}
                      </span>
                      <span className="text-xs text-neutral-500">
                        Balance: ₹{entry.balanceAfter.toFixed(2)}
                      </span>
                    </div>
                  </div>
                </Card>
              </li>
            );
          })}
        </ul>
      )}
    </main>
  );
}
