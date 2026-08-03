"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { ErrorState, NotYetAvailable } from "@/components/states";
import {
  Button,
  Card,
  EmptyState,
  Skeleton,
  TBody,
  TD,
  TH,
  THead,
  TR,
  Table,
  Tabs,
  cx,
} from "@/components/ui";
import { isNotImplemented } from "@/lib/api";
import { isoDateOffsetFromToday, toLocalIsoDate } from "@/lib/date";
import { listEarningsLedger } from "@/lib/earnings-api";
import { EarningEntryType, earningSourceLabel } from "@/lib/earnings-types";
import { formatDateTime, formatInr, formatSignedInr } from "@/lib/format";
import type { EarningLedgerEntry } from "@/lib/earnings-types";

type Period = "30" | "90" | "all";

const PERIOD_TABS = [
  { value: "30", label: "Last 30 days" },
  { value: "90", label: "Last 90 days" },
  { value: "all", label: "All time" },
] as const satisfies readonly { value: Period; label: string }[];

/**
 * The append-only earning ledger: a credit per completed job, a debit for a
 * penalty or a clawback.
 *
 * Two things this screen previously got wrong and must not get wrong again:
 *
 * 1. A debit rendered exactly like a credit of the same size, so a ₹500
 *    penalty read as ₹500 earned. Direction now comes from `entryType` and is
 *    carried by both the sign (`formatSignedInr`) and the colour, because
 *    colour alone is not an accessible way to say "this went the other way".
 * 2. Amounts were plain proportional digits. Every money column here is
 *    `.nums` so a column of figures lines up as it scrolls.
 *
 * The period filter is client-side — `GET /earnings/ledger` takes no range
 * parameters, so this narrows what has already been fetched rather than
 * changing the request.
 */
export function LedgerSection() {
  const [period, setPeriod] = useState<Period>("all");
  const query = useQuery({ queryKey: ["partner-earnings-ledger"], queryFn: listEarningsLedger });

  if (query.isPending) {
    return (
      <Card title="Ledger">
        <LedgerSkeleton />
      </Card>
    );
  }

  if (query.isError && isNotImplemented(query.error)) {
    return (
      <Card title="Ledger">
        <NotYetAvailable
          title="Your ledger isn't available yet"
          description="Earnings tracking is still being built on the platform side. Every completed job will be listed here once it goes live."
          action={
            <Link href="/jobs">
              <Button variant="secondary">See your jobs</Button>
            </Link>
          }
        />
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Ledger">
        <ErrorState
          title="Couldn't load your ledger"
          error={query.error}
          onRetry={() => query.refetch()}
          isRetrying={query.isRefetching}
        />
      </Card>
    );
  }

  const entries = query.data;

  if (entries.length === 0) {
    return (
      <Card title="Ledger">
        <EmptyState
          title="No earnings yet"
          description="Every job you complete is credited here, along with any adjustments. Accept a job to get started."
          action={
            <Link href="/jobs">
              <Button variant="secondary">See your jobs</Button>
            </Link>
          }
        />
      </Card>
    );
  }

  const visible = visibleEntries(entries, period);

  return (
    <Card
      title="Ledger"
      description="Every credit and debit on your account, newest first."
    >
      <Tabs
        tabs={PERIOD_TABS}
        value={period}
        onChange={setPeriod}
        label="Ledger period"
        className="mb-4"
      />

      {visible.length === 0 ? (
        <EmptyState
          title="Nothing in this period"
          description="You have earlier entries — widen the period to see them."
          action={
            <Button variant="secondary" onClick={() => setPeriod("all")}>
              Show all time
            </Button>
          }
        />
      ) : (
        <>
          {/* Phone: one stacked row per entry, amount on the right where a
              thumb-scrolling eye already is. */}
          <ul className="flex flex-col divide-y divide-line sm:hidden">
            {visible.map((entry) => (
              <li key={entry.id} className="flex items-start justify-between gap-4 py-3">
                <div className="min-w-0">
                  <p className="text-sm font-medium text-fg">
                    {earningSourceLabel(entry.sourceType)}
                  </p>
                  {entry.description ? (
                    <p className="mt-0.5 line-clamp-2 text-sm leading-relaxed text-fg-muted">
                      {entry.description}
                    </p>
                  ) : null}
                  <p className="nums mt-0.5 text-xs text-fg-subtle">
                    {formatDateTime(entry.createdAtUtc)}
                  </p>
                </div>
                <div className="shrink-0 text-right">
                  <p className={cx("nums text-sm font-semibold", amountToneFor(entry))}>
                    {signedAmount(entry)}
                  </p>
                  <p className="nums mt-0.5 text-xs text-fg-subtle">
                    {formatInr(entry.balanceAfter)}
                  </p>
                </div>
              </li>
            ))}
          </ul>

          {/* Tablet and up: the same data as a table, where the columns fit. */}
          <div className="hidden sm:block">
            <Table>
              <THead>
                <TR>
                  <TH>Date</TH>
                  <TH>Activity</TH>
                  <TH numeric>Amount</TH>
                  <TH numeric>Balance</TH>
                </TR>
              </THead>
              <TBody>
                {visible.map((entry) => (
                  <TR key={entry.id}>
                    <TD className="nums whitespace-nowrap text-fg-muted">
                      {formatDateTime(entry.createdAtUtc)}
                    </TD>
                    <TD>
                      <span className="font-medium text-fg">
                        {earningSourceLabel(entry.sourceType)}
                      </span>
                      {entry.description ? (
                        <span className="mt-0.5 block text-fg-muted">{entry.description}</span>
                      ) : null}
                    </TD>
                    <TD numeric className={cx("font-semibold", amountToneFor(entry))}>
                      {signedAmount(entry)}
                    </TD>
                    <TD numeric className="text-fg-muted">
                      {formatInr(entry.balanceAfter)}
                    </TD>
                  </TR>
                ))}
              </TBody>
            </Table>
          </div>
        </>
      )}
    </Card>
  );
}

const isDebit = (entry: EarningLedgerEntry) => entry.entryType === EarningEntryType.Debit;

/** "−₹500.00" for a debit, "+₹500.00" for a credit — never the bare magnitude. */
const signedAmount = (entry: EarningLedgerEntry) =>
  formatSignedInr(entry.amount, isDebit(entry));

const amountToneFor = (entry: EarningLedgerEntry) => (isDebit(entry) ? "text-danger" : "text-success");

/**
 * Narrows to entries within `period` days of today and orders them newest
 * first. The ordering is applied here rather than assumed: the endpoint makes
 * no promise about it, and a ledger read in any other order is unreadable.
 *
 * The window is compared as *local calendar dates* via
 * `isoDateOffsetFromToday` rather than a `toISOString` slice — the latter
 * converts to UTC first and, in IST before 05:30, silently drops a day off the
 * boundary.
 */
function visibleEntries(entries: EarningLedgerEntry[], period: Period): EarningLedgerEntry[] {
  // Inclusive of today, so "last 30 days" is today plus the 29 before it.
  const cutoff = period === "all" ? null : isoDateOffsetFromToday(-(Number(period) - 1));

  const withinPeriod =
    cutoff === null
      ? entries
      : entries.filter((entry) => {
          if (!entry.createdAtUtc) return false;
          const entryDate = new Date(entry.createdAtUtc);
          if (Number.isNaN(entryDate.getTime())) return false;
          return toLocalIsoDate(entryDate) >= cutoff;
        });

  return [...withinPeriod].sort((a, b) => (a.createdAtUtc < b.createdAtUtc ? 1 : -1));
}

/** Row-shaped placeholder so the card does not resize when entries land. */
function LedgerSkeleton() {
  return (
    <div className="flex flex-col gap-3" aria-hidden>
      <Skeleton className="h-9 w-full rounded-lg" />
      {Array.from({ length: 4 }, (_, index) => (
        <div key={index} className="flex items-start justify-between gap-4 py-1">
          <div className="min-w-0 flex-1">
            <Skeleton className="h-4 w-32" />
            <Skeleton className="mt-2 h-3 w-44" />
          </div>
          <Skeleton className="h-4 w-20" />
        </div>
      ))}
    </div>
  );
}
