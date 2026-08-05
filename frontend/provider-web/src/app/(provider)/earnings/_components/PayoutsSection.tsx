"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { ErrorState, NotYetAvailable } from "@/components/states";
import {
  Card,
  EmptyState,
  Skeleton,
  TBody,
  TD,
  TH,
  THead,
  TR,
  Table,
} from "@/components/ui";
import { isNotImplemented } from "@/lib/api";
import { listPayouts } from "@/lib/earnings-api";
import { formatInr, formatIsoDate } from "@/lib/format";
import { PayoutStatusBadge } from "./PayoutStatusBadge";
import type { PayoutSummary } from "@/lib/earnings-types";

/**
 * Payout batches (docs/PROVIDER.md's `provider_payout` table — a manual bank
 * transfer in v1, so a provider reads this to answer "has it been sent yet").
 *
 * `periodStart`/`periodEnd` arrive as `YYYY-MM-DD` calendar dates and are
 * rendered through `formatIsoDate`, which parses them as local date parts.
 * `new Date("2026-08-03")` would treat them as UTC midnight and show the
 * previous day everywhere east of Greenwich.
 */
export function PayoutsSection() {
  const query = useQuery({ queryKey: ["provider-payouts"], queryFn: listPayouts });

  if (query.isPending) {
    return (
      <Card title="Payouts">
        <PayoutsSkeleton />
      </Card>
    );
  }

  if (query.isError && isNotImplemented(query.error)) {
    return (
      <Card title="Payouts">
        <NotYetAvailable
          title="Payouts aren't available yet"
          description="Payout batches are still being built on the platform side. Once they go live, every transfer to your bank account will be listed here."
        />
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Payouts">
        <ErrorState
          title="Couldn't load your payouts"
          error={query.error}
          onRetry={() => query.refetch()}
          isRetrying={query.isRefetching}
        />
      </Card>
    );
  }

  if (query.data.length === 0) {
    return (
      <Card title="Payouts">
        <EmptyState
          title="No payouts yet"
          description="Your earnings are paid out in batches to your registered bank account. The first one will appear here once it's raised."
        />
      </Card>
    );
  }

  const payouts = [...query.data].sort((a, b) => (a.periodStart < b.periodStart ? 1 : -1));

  return (
    <Card
      title="Payouts"
      description="Transfers to your registered bank account, newest first."
    >
      {/* Phone: the whole row is a link, sized to be tapped one-handed. */}
      <ul className="flex flex-col gap-2.5 sm:hidden">
        {payouts.map((payout) => (
          <li key={payout.id}>
            <PayoutCard payout={payout} />
          </li>
        ))}
      </ul>

      <div className="hidden sm:block">
        <Table>
          <THead>
            <TR>
              <TH>Period</TH>
              <TH numeric>Amount</TH>
              <TH>Status</TH>
              <TH>
                <span className="sr-only">Actions</span>
              </TH>
            </TR>
          </THead>
          <TBody>
            {payouts.map((payout) => (
              <TR key={payout.id}>
                <TD className="nums whitespace-nowrap text-fg-muted">{periodLabel(payout)}</TD>
                <TD numeric className="font-semibold">
                  {formatInr(payout.totalAmount)}
                </TD>
                <TD>
                  <PayoutStatusBadge status={payout.status} />
                </TD>
                <TD className="text-right">
                  <Link
                    href={`/earnings/payouts/${payout.id}`}
                    className="rounded-lg text-sm font-medium text-brand-600 underline-offset-4 transition-colors duration-fast ease-out hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600/40 dark:text-brand-400"
                  >
                    View
                    <span className="sr-only"> payout for {periodLabel(payout)}</span>
                  </Link>
                </TD>
              </TR>
            ))}
          </TBody>
        </Table>
      </div>
    </Card>
  );
}

/** One payout as a full-width tap target, for the phone layout. */
function PayoutCard({ payout }: { payout: PayoutSummary }) {
  return (
    <Link
      href={`/earnings/payouts/${payout.id}`}
      className="flex items-center justify-between gap-3 rounded-xl border border-line bg-surface-2 p-3.5 transition duration-fast ease-out hover:border-line-strong hover:bg-surface-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-600/40"
    >
      <div className="min-w-0">
        <p className="nums text-sm font-semibold text-fg">{formatInr(payout.totalAmount)}</p>
        <p className="nums mt-0.5 text-xs text-fg-muted">{periodLabel(payout)}</p>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <PayoutStatusBadge status={payout.status} />
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          className="h-4 w-4 text-fg-subtle"
          aria-hidden
        >
          <path d="m9 18 6-6-6-6" />
        </svg>
      </div>
    </Link>
  );
}

/** "1 Jul 2026 – 31 Jul 2026", or the single day when the batch covers one. */
export function periodLabel(payout: { periodStart: string; periodEnd: string }): string {
  if (!payout.periodStart || !payout.periodEnd) return "—";
  if (payout.periodStart === payout.periodEnd) return formatIsoDate(payout.periodStart);
  return `${formatIsoDate(payout.periodStart)} – ${formatIsoDate(payout.periodEnd)}`;
}

/** Matches the real row heights so the card does not resize when data lands. */
function PayoutsSkeleton() {
  return (
    <div className="flex flex-col gap-2.5" aria-hidden>
      {Array.from({ length: 3 }, (_, index) => (
        <div
          key={index}
          className="flex items-center justify-between gap-3 rounded-xl border border-line bg-surface-2 p-3.5"
        >
          <div>
            <Skeleton className="h-4 w-24" />
            <Skeleton className="mt-2 h-3 w-40" />
          </div>
          <Skeleton className="h-5 w-16 rounded-full" />
        </div>
      ))}
    </div>
  );
}
