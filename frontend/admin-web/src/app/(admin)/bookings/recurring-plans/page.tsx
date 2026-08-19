"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { motion } from "motion/react";
import { useState } from "react";
import { Reveal, revealItem } from "@/components/motion";
import { Badge, Button, Card, Field, PageHeading, Select, Skeleton, StatTile } from "@/components/ui";
import { DataTable, FilterBar, Pagination, countActiveFilters, formatDate } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { BookingsTabs } from "@/components/BookingsTabs";
import {
  FREQUENCY_LABELS,
  PLAN_STATUS_LABELS,
  RecurrenceFrequency,
  RecurringPlanStatus,
  describeCadence,
  getRecurringPlanReport,
  searchRecurringPlans,
} from "../_lib/recurring-plans-api";
import type { RecurringPlanListItem } from "../_lib/recurring-plans-api";

const PAGE_SIZE = 20;

const STATUS_OPTIONS = [
  { value: "", label: "Any status" },
  { value: String(RecurringPlanStatus.Active), label: "Active" },
  { value: String(RecurringPlanStatus.Paused), label: "Paused" },
  { value: String(RecurringPlanStatus.Cancelled), label: "Cancelled" },
  { value: String(RecurringPlanStatus.Completed), label: "Completed" },
];

const FREQUENCY_OPTIONS = [
  { value: "", label: "Any cadence" },
  { value: String(RecurrenceFrequency.Weekly), label: "Weekly" },
  { value: String(RecurrenceFrequency.Biweekly), label: "Every 2 weeks" },
  { value: String(RecurrenceFrequency.Monthly), label: "Monthly" },
];

const STATUS_TONES: Record<RecurringPlanStatus, "success" | "warning" | "neutral"> = {
  [RecurringPlanStatus.Active]: "success",
  [RecurringPlanStatus.Paused]: "warning",
  [RecurringPlanStatus.Cancelled]: "neutral",
  [RecurringPlanStatus.Completed]: "neutral",
};

interface FilterFormState {
  status: string;
  frequency: string;
}

const EMPTY_FILTERS: FilterFormState = { status: "", frequency: "" };

/**
 * Admin visibility into recurring booking plans (task 299,
 * PRODUCT-ENHANCEMENTS.md section 2), mirroring the config/report shape the
 * Coupon and Nestly Coins screens already use: aggregate tiles on top, the
 * per-record list underneath.
 *
 * Read-only. There is deliberately no admin pause/cancel here - a plan is the
 * customer's standing instruction, and an admin who needs to stop the work it
 * generates acts on the individual bookings on the "All bookings" tab, which
 * already audits every such action.
 *
 * The two volume tiles are separate on purpose and the wording says why: one
 * counts bookings that exist, the other counts plans the scheduler has not
 * reached yet. Adding them together would present a projection as a fact.
 */
export default function RecurringPlansPage() {
  const [filters, setFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<FilterFormState>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");

  const reportQuery = useQuery({
    queryKey: ["recurring-plans", "report", fromDate, toDate] as const,
    queryFn: () => getRecurringPlanReport(fromDate || undefined, toDate || undefined),
  });

  const listQuery = useQuery({
    queryKey: ["recurring-plans", "list", appliedFilters, page] as const,
    queryFn: () =>
      searchRecurringPlans({
        status: appliedFilters.status || undefined,
        frequency: appliedFilters.frequency || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: keepPreviousData,
  });

  const onSubmit = () => {
    setAppliedFilters(filters);
    setPage(1);
  };

  const onClear = () => {
    setFilters(EMPTY_FILTERS);
    setAppliedFilters(EMPTY_FILTERS);
    setPage(1);
  };

  const report = reportQuery.data;
  const countFor = (status: RecurringPlanStatus) =>
    report?.byStatus.find((row) => row.status === status)?.planCount ?? 0;

  const columns: DataTableColumn<RecurringPlanListItem>[] = [
    {
      key: "customer",
      header: "Customer",
      cell: (plan) => <span className="font-medium text-fg">{plan.customerName}</span>,
    },
    {
      key: "service",
      header: "Service",
      cell: (plan) => plan.serviceName,
    },
    {
      key: "cadence",
      header: "Cadence",
      cell: (plan) => describeCadence(plan),
    },
    {
      key: "status",
      header: "Status",
      cell: (plan) => (
        <Badge tone={STATUS_TONES[plan.status] ?? "neutral"}>
          {PLAN_STATUS_LABELS[plan.status] ?? String(plan.status)}
        </Badge>
      ),
    },
    {
      key: "progress",
      header: "Delivered",
      numeric: true,
      sortValue: (plan) => plan.completedOccurrenceCount,
      cell: (plan) => (
        <span className="nums">
          {plan.completedOccurrenceCount}
          {plan.occurrenceCount === null ? "" : ` / ${plan.occurrenceCount}`}
        </span>
      ),
    },
    {
      key: "next",
      header: "Next occurrence",
      sortValue: (plan) => plan.nextOccurrenceDate,
      cell: (plan) =>
        // Only an active plan actually has a next visit. Paused, cancelled and
        // completed plans still carry the column in the database, and showing
        // it would read as a commitment nobody is going to keep.
        plan.status === RecurringPlanStatus.Active ? (
          <span className="nums">{formatDate(plan.nextOccurrenceDate)}</span>
        ) : (
          <span className="text-fg-subtle">—</span>
        ),
    },
    {
      key: "created",
      header: "Created",
      sortValue: (plan) => plan.createdAtUtc,
      cell: (plan) => <span className="nums">{formatDate(plan.createdAtUtc)}</span>,
    },
  ];

  return (
    <div className="mx-auto w-full max-w-7xl">
      <PageHeading
        title="Bookings"
        subtitle="Recurring plans: the standing instructions behind repeat jobs, and the work they are about to generate."
      />

      <BookingsTabs />

      <div className="flex flex-col gap-6">
        {report ? (
          <Reveal className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            <motion.div variants={revealItem}>
              <StatTile label="Active plans" value={countFor(RecurringPlanStatus.Active).toLocaleString("en-IN")} />
            </motion.div>
            <motion.div variants={revealItem}>
              <StatTile label="Paused plans" value={countFor(RecurringPlanStatus.Paused).toLocaleString("en-IN")} />
            </motion.div>
            <motion.div variants={revealItem}>
              <StatTile
                label="Cancelled plans"
                value={countFor(RecurringPlanStatus.Cancelled).toLocaleString("en-IN")}
              />
            </motion.div>
            <motion.div variants={revealItem}>
              <StatTile
                label="Bookings already scheduled"
                value={report.upcomingOccurrenceVolume.toLocaleString("en-IN")}
                hint={`Generated by a plan, with a slot between ${formatDate(report.horizonFromDate)} and ${formatDate(report.horizonToDate)}.`}
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
          title="Upcoming occurrence volume"
          description="Scope the horizon. Leave both dates empty for the next four weeks."
        >
          <div className="flex flex-wrap items-end gap-4">
            <div className="w-44">
              <Field
                label="From"
                type="date"
                max={toDate || undefined}
                value={fromDate}
                onChange={(event) => setFromDate(event.target.value)}
              />
            </div>
            <div className="w-44">
              <Field
                label="To"
                type="date"
                min={fromDate || undefined}
                value={toDate}
                onChange={(event) => setToDate(event.target.value)}
              />
            </div>
            {fromDate || toDate ? (
              <Button
                type="button"
                variant="secondary"
                onClick={() => {
                  setFromDate("");
                  setToDate("");
                }}
              >
                Reset horizon
              </Button>
            ) : null}
          </div>

          {report ? (
            <div className="mt-5 flex flex-col gap-4">
              <p className="text-sm text-fg-muted">
                <span className="nums font-medium text-fg">
                  {report.plansDueInHorizon.toLocaleString("en-IN")}
                </span>{" "}
                active {report.plansDueInHorizon === 1 ? "plan is" : "plans are"} due in this window but
                have not been generated into bookings yet — the scheduler reaches them closer to the date,
                and an occurrence can still be skipped if no slot is available.
              </p>

              <div className="flex flex-wrap gap-2">
                {report.activeByFrequency.map((row) => (
                  <Badge key={row.frequency} tone="neutral">
                    {FREQUENCY_LABELS[row.frequency] ?? String(row.frequency)}:{" "}
                    <span className="nums">{row.planCount}</span>
                  </Badge>
                ))}
              </div>

              {report.upcomingVolumeByDate.length > 0 ? (
                <ul className="flex flex-wrap gap-2">
                  {report.upcomingVolumeByDate.map((row) => (
                    <li
                      key={row.slotDate}
                      className="rounded-lg border border-line bg-surface-2 px-3 py-1.5 text-sm text-fg-muted"
                    >
                      <span className="nums">{formatDate(row.slotDate)}</span>{" "}
                      <span className="nums font-medium text-fg">{row.bookingCount}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-sm text-fg-subtle">
                  No recurring bookings are scheduled inside this window yet.
                </p>
              )}
            </div>
          ) : (
            <div className="mt-5 flex flex-col gap-3">
              <Skeleton className="h-4 w-3/4" />
              <Skeleton className="h-4 w-1/2" />
            </div>
          )}
        </Card>

        <div>
          <FilterBar
            onSubmit={onSubmit}
            onClear={onClear}
            activeCount={countActiveFilters(appliedFilters)}
            busy={listQuery.isFetching}
          >
            <Select
              label="Status"
              options={STATUS_OPTIONS}
              value={filters.status}
              onChange={(e) => setFilters((f) => ({ ...f, status: e.target.value }))}
            />
            <Select
              label="Cadence"
              options={FREQUENCY_OPTIONS}
              value={filters.frequency}
              onChange={(e) => setFilters((f) => ({ ...f, frequency: e.target.value }))}
            />
          </FilterBar>

          <div className="mt-6 flex flex-col gap-4">
            <DataTable
              title="Recurring plans"
              description="Every standing instruction on the platform, newest first."
              columns={columns}
              rows={listQuery.data?.items}
              rowKey={(plan) => plan.id}
              isLoading={listQuery.isPending}
              isFetching={listQuery.isFetching}
              error={listQuery.error}
              onRetry={() => listQuery.refetch()}
              skeletonRows={8}
              minWidth="880px"
              caption="Recurring booking plans matching the current filters"
              emptyTitle="No recurring plans match these filters"
              emptyDescription="Clear the filters to see every plan on the platform."
              emptyAction={
                <Button variant="secondary" onClick={onClear}>
                  Clear filters
                </Button>
              }
            />

            {listQuery.data && listQuery.data.totalCount > 0 ? (
              <Pagination
                page={listQuery.data.page}
                pageSize={listQuery.data.pageSize}
                totalCount={listQuery.data.totalCount}
                onPageChange={setPage}
                itemLabel="plan"
                busy={listQuery.isFetching}
              />
            ) : null}
          </div>
        </div>
      </div>
    </div>
  );
}

/** Matches a `StatTile`'s height so the tiles do not jump when the report lands. */
function StatTileSkeleton() {
  return (
    <div className="rounded-2xl border border-line bg-surface p-5 shadow-sm">
      <Skeleton className="h-4 w-28" />
      <Skeleton className="mt-3 h-9 w-16" />
    </div>
  );
}
