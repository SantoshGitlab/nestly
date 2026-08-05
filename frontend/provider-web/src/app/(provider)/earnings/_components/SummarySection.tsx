"use client";

import { useQuery } from "@tanstack/react-query";
import { ErrorState, NotYetAvailable } from "@/components/states";
import { Card, Skeleton } from "@/components/ui";
import { isNotImplemented } from "@/lib/api";
import { getEarningsSummary } from "@/lib/earnings-api";
import { formatDateTime, formatInr } from "@/lib/format";
import type { EarningLedgerEntry } from "@/lib/earnings-types";

/**
 * The provider's balance, as the first thing on the earnings screen.
 *
 * Rendered as one large number on a brand panel rather than a row of tiles:
 * "how much am I owed" is the single question this screen exists to answer,
 * and on a phone anything else at the top pushes it below the fold.
 *
 * Nothing here is derived from `summary.entries` beyond the most recent
 * timestamp. The response carries a list of entries, but nothing in the
 * contract promises it is the *complete* ledger, so totalling it would risk
 * showing a confidently wrong number next to an authoritative one.
 */
export function SummarySection() {
  const query = useQuery({ queryKey: ["provider-earnings-summary"], queryFn: getEarningsSummary });

  if (query.isPending) {
    return <SummarySkeleton />;
  }

  if (query.isError && isNotImplemented(query.error)) {
    return (
      <Card title="Balance">
        <NotYetAvailable
          title="Earnings aren't available yet"
          description="Earnings tracking is still being built on the platform side. Your completed jobs are recorded — the balance will appear here once payouts go live."
        />
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Balance">
        <ErrorState
          title="Couldn't load your balance"
          error={query.error}
          onRetry={() => query.refetch()}
          isRetrying={query.isRefetching}
        />
      </Card>
    );
  }

  const lastActivity = latestEntryTimestamp(query.data.entries);

  return (
    <section className="animate-fade-in overflow-hidden rounded-2xl bg-brand-gradient p-6 shadow-brand sm:p-7">
      <p className="text-sm font-medium text-fg-on-brand/80">Current balance</p>
      <p className="nums mt-1 text-display-md font-semibold text-fg-on-brand">
        {formatInr(query.data.currentBalance)}
      </p>
      <p className="mt-3 text-xs leading-relaxed text-fg-on-brand/75">
        {lastActivity
          ? `Last recorded activity ${formatDateTime(lastActivity)}.`
          : "Credits for completed jobs and any adjustments show up here."}
      </p>
    </section>
  );
}

/** The newest `createdAtUtc` in the list — the API does not promise an order. */
function latestEntryTimestamp(entries: EarningLedgerEntry[]): string | null {
  let latest: string | null = null;
  for (const entry of entries) {
    if (!entry.createdAtUtc) continue;
    if (latest === null || entry.createdAtUtc > latest) latest = entry.createdAtUtc;
  }
  return latest;
}

/** Same footprint as the real panel so the page does not jump when it lands. */
function SummarySkeleton() {
  return (
    <div
      className="overflow-hidden rounded-2xl border border-line bg-surface p-6 shadow-sm sm:p-7"
      aria-hidden
    >
      <Skeleton className="h-4 w-28" />
      <Skeleton className="mt-2 h-10 w-48" />
      <Skeleton className="mt-4 h-3 w-56" />
    </div>
  );
}
