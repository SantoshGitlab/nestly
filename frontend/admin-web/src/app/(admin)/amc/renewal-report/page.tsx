"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { motion } from "motion/react";
import { useState } from "react";
import { BookingsTabs } from "@/components/BookingsTabs";
import { Reveal, revealItem } from "@/components/motion";
import { SectionError } from "@/components/screen-states";
import { Badge, Card, Field, PageHeading, Skeleton, StatTile } from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { DataTable, FilterBar, countActiveFilters, formatDate } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { endOfLocalDayUtc, startOfLocalDayUtc } from "@/lib/day-range";
import {
  CONTRACT_STATUS_LABELS,
  CustomerAmcContractStatus,
  getAmcRenewalReport,
} from "../_lib/amc-api";
import type { AmcContractAdminListItemResponse } from "../_lib/amc-api";

interface DateRangeFilters {
  fromDate: string;
  toDate: string;
}

const EMPTY_FILTERS: DateRangeFilters = { fromDate: "", toDate: "" };

const STATUS_TONES: Record<CustomerAmcContractStatus, BadgeTone> = {
  [CustomerAmcContractStatus.Active]: "success",
  [CustomerAmcContractStatus.Exhausted]: "warning",
  [CustomerAmcContractStatus.Expired]: "neutral",
  [CustomerAmcContractStatus.Cancelled]: "neutral",
};

/**
 * The AMC renewal-pipeline report (docs/AMC.md - "AMC has the best
 * cash-flow profile in the catalogue" only realises if expiring/exhausted
 * contracts actually get renewed). Mirrors the tiles-plus-list shape
 * `RecurringPlansPage`'s report section established, using the
 * local-day-to-UTC-instant range conversion `referral/reports/page.tsx`
 * uses rather than recurring-plans' `DateOnly` horizon — `GetRenewalReportAsync`
 * takes full `DateTime?` bounds, not a slot date.
 */
export default function AmcRenewalReportPage() {
  const [filters, setFilters] = useState<DateRangeFilters>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<DateRangeFilters>(EMPTY_FILTERS);
  const [rangeError, setRangeError] = useState<string | null>(null);

  const range = {
    fromUtc: appliedFilters.fromDate ? startOfLocalDayUtc(appliedFilters.fromDate) ?? undefined : undefined,
    toUtc: appliedFilters.toDate ? endOfLocalDayUtc(appliedFilters.toDate) ?? undefined : undefined,
  };

  const reportQuery = useQuery({
    queryKey: ["amc-renewal-report", appliedFilters] as const,
    queryFn: () => getAmcRenewalReport(range),
    placeholderData: keepPreviousData,
  });

  const applyFilters = () => {
    if (filters.fromDate && filters.toDate && filters.toDate < filters.fromDate) {
      setRangeError("The end date cannot be before the start date.");
      return;
    }
    setRangeError(null);
    setAppliedFilters(filters);
  };

  const clearFilters = () => {
    setRangeError(null);
    setFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
  };

  const report = reportQuery.data;
  const countFor = (status: CustomerAmcContractStatus) =>
    report?.byStatus.find((row) => row.status === status)?.contractCount ?? 0;

  const columns: DataTableColumn<AmcContractAdminListItemResponse>[] = [
    {
      key: "customer",
      header: "Customer",
      cell: (contract) => <span className="font-medium text-fg">{contract.customerName}</span>,
    },
    {
      key: "plan",
      header: "Plan",
      cell: (contract) => contract.planName,
    },
    {
      key: "asset",
      header: "Asset",
      cell: (contract) => contract.assetLabel,
    },
    {
      key: "status",
      header: "Status",
      cell: (contract) => (
        <Badge tone={STATUS_TONES[contract.status] ?? "neutral"}>
          {CONTRACT_STATUS_LABELS[contract.status] ?? String(contract.status)}
        </Badge>
      ),
    },
    {
      key: "visits",
      header: "Visits",
      numeric: true,
      sortValue: (contract) => contract.visitsRemaining,
      cell: (contract) => (
        <span className="nums">
          {contract.visitsRemaining} / {contract.visitsIncluded}
        </span>
      ),
    },
    {
      key: "end",
      header: "Cover ends",
      sortValue: (contract) => contract.endDateUtc,
      cell: (contract) => <span className="nums">{formatDate(contract.endDateUtc)}</span>,
    },
  ];

  return (
    <div className="w-full max-w-7xl">
      <PageHeading
        title="Bookings"
        subtitle="AMC renewal pipeline: contracts expiring or exhausted within a horizon — the customers worth a renewal conversation."
      />

      <BookingsTabs />

      <div className="flex flex-col gap-6">
        {report ? (
          <Reveal className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <motion.div variants={revealItem}>
              <StatTile tone="brand" label="Total contracts" value={report.totalContracts.toLocaleString("en-IN")} />
            </motion.div>
            <motion.div variants={revealItem}>
              <StatTile tone="success" label="Active" value={countFor(CustomerAmcContractStatus.Active).toLocaleString("en-IN")} />
            </motion.div>
            <motion.div variants={revealItem}>
              <StatTile
                tone="info"
                label="Exhausted"
                value={countFor(CustomerAmcContractStatus.Exhausted).toLocaleString("en-IN")}
                hint="Used every visit — a customer who got full value."
              />
            </motion.div>
            <motion.div variants={revealItem}>
              <StatTile
                tone="danger"
                label="Expired"
                value={countFor(CustomerAmcContractStatus.Expired).toLocaleString("en-IN")}
                hint="Term ran out with visits still unused."
              />
            </motion.div>
          </Reveal>
        ) : (
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            {Array.from({ length: 4 }, (_, index) => (
              <StatTileSkeleton key={index} />
            ))}
          </div>
        )}

        <Card
          title="Renewal horizon"
          description="Contracts expiring or already exhausted inside this window. Leave both dates empty for the next 30 days."
        >
          <FilterBar
            columns={2}
            submitLabel="Apply range"
            onSubmit={applyFilters}
            onClear={clearFilters}
            activeCount={countActiveFilters(appliedFilters)}
            busy={reportQuery.isFetching}
          >
            <Field
              label="From"
              type="date"
              max={filters.toDate || undefined}
              value={filters.fromDate}
              error={rangeError ?? undefined}
              onChange={(event) => setFilters((current) => ({ ...current, fromDate: event.target.value }))}
            />
            <Field
              label="To"
              type="date"
              min={filters.fromDate || undefined}
              value={filters.toDate}
              onChange={(event) => setFilters((current) => ({ ...current, toDate: event.target.value }))}
            />
          </FilterBar>

          {reportQuery.isPending ? (
            <div className="mt-5 flex flex-col gap-3">
              <Skeleton className="h-4 w-3/4" />
              <Skeleton className="h-4 w-1/2" />
            </div>
          ) : reportQuery.error ? (
            <div className="mt-5">
              <SectionError error={reportQuery.error} onRetry={() => reportQuery.refetch()} />
            </div>
          ) : report ? (
            <p className="mt-5 text-sm text-fg-muted">
              <span className="nums font-medium text-fg">{report.expiringInHorizon.toLocaleString("en-IN")}</span>{" "}
              contract{report.expiringInHorizon === 1 ? "" : "s"} expiring, and{" "}
              <span className="nums font-medium text-fg">{report.exhaustedInHorizon.toLocaleString("en-IN")}</span>{" "}
              already exhausted, between <span className="nums">{formatDate(report.horizonFromUtc)}</span> and{" "}
              <span className="nums">{formatDate(report.horizonToUtc)}</span>.
            </p>
          ) : null}
        </Card>

        <DataTable
          title="Contracts to follow up"
          description="Expiring or exhausted inside the horizon above — reach out before the cover lapses unrenewed."
          columns={columns}
          rows={report?.expiringOrExhaustedContracts}
          rowKey={(contract) => contract.id}
          isLoading={reportQuery.isPending}
          isFetching={reportQuery.isFetching}
          error={reportQuery.error}
          onRetry={() => reportQuery.refetch()}
          skeletonRows={6}
          minWidth="880px"
          caption="AMC contracts expiring or exhausted within the selected horizon"
          emptyTitle="Nothing expiring or exhausted in this horizon"
          emptyDescription="Every contract in range still has visits left and time on the clock."
        />
      </div>
    </div>
  );
}

/** Matches a `StatTile`'s height so the tiles do not jump when the report lands. */
function StatTileSkeleton() {
  return (
    <div className="rounded-2xl bg-surface p-5 shadow-sm">
      <Skeleton className="h-4 w-28" />
      <Skeleton className="mt-3 h-9 w-16" />
    </div>
  );
}
