"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import type { ReactNode } from "react";
import {
  Alert,
  AreaChart,
  Button,
  Card,
  CHART_TONES,
  DonutChart,
  EmptyState,
  KpiCard,
  PageHeading,
  Skeleton,
} from "@/components/ui";
import type { ChartTone } from "@/components/ui";
import { Breadcrumbs } from "@/components/data-table";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { getFulfilmentBoard } from "@/lib/bookings-api";
import { BookingStatus } from "@/lib/types";
import type { CustomerAnalyticsResponse } from "@/lib/types";
import { getPaymentReconciliation } from "@/lib/payments-api";
import { PaymentReconciliationCategory } from "@/lib/payments-types";
import { getProviderOnboardingOverview } from "@/lib/providers-api";
import { listCatalogHealthIssues } from "@/lib/catalog-api";
import type { CatalogHealthReason } from "@/lib/catalog-types";
import { listServiceabilityCoverageGaps, listUnmappedActiveServices } from "@/lib/serviceability-api";
import { todayIsoDate } from "@/lib/date";

/**
 * Executive Overview (new admin-web page, per the admin's request: "every
 * module has an overview - counts, a graph, and genuinely insightful
 * information - click to see detailed data").
 *
 * This is a higher-altitude, cross-module composition layer, not a new
 * source of truth: every count, chart and insight sentence below is computed
 * from the SAME per-domain endpoints their own detailed pages already use -
 * `GetFulfilmentBoard`, `GetReconciliation`, `GetOnboardingOverview`,
 * `GetAnalytics` (customers), `ListHealth` (catalog), and the serviceability
 * coverage-gap/unmapped-service endpoints. No new backend endpoint was added:
 * every number a genuinely useful admin dashboard needs already exists
 * somewhere in these responses (see each section's own comment for exactly
 * which field it reads). `/dashboard` keeps its own purpose (the
 * date-filtered operational KPI/booking-list view) - this page sits
 * alongside it as a cross-module summary, one click from the sidebar.
 *
 * Six independent `useQuery` calls fire in parallel (React Query handles
 * that natively) rather than one combined request, so each section's
 * loading/error/empty state is its own - a failure in, say, Payments never
 * takes down Bookings, Providers, or any other section on this page.
 */
export default function ExecutiveOverviewPage() {
  return (
    <div className="w-full max-w-7xl">
      <PageHeading
        title="Executive Overview"
        subtitle="A cross-module snapshot - bookings, payments, providers, customers, catalog and serviceability at a glance."
        breadcrumbs={<Breadcrumbs items={[{ label: "Home", href: "/dashboard" }, { label: "Executive Overview" }]} />}
      />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <BookingsSection />
        <PaymentsSection />
        <ProvidersSection />
        <CustomersSection />
        <CatalogSection />
        <ServiceabilitySection />
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Shared section chrome                                                     */
/* -------------------------------------------------------------------------- */

function SectionSkeleton() {
  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-4">
        <Skeleton className="h-24 rounded-2xl" />
        <Skeleton className="h-24 rounded-2xl" />
      </div>
      <Skeleton className="h-40 rounded-xl" />
      <Skeleton className="h-4 w-3/4" />
    </div>
  );
}

function SectionError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  return (
    <Alert
      tone="error"
      title="Could not load this section"
      action={
        <Button size="sm" variant="secondary" onClick={onRetry}>
          Retry
        </Button>
      }
    >
      {describeError(error)}
    </Alert>
  );
}

/** The one genuinely-computed insight sentence per section (never filler text). */
function Insight({ children }: { children: ReactNode }) {
  return (
    <p className="rounded-xl bg-info-soft px-4 py-3 text-sm leading-relaxed text-fg">
      <InsightIcon /> {children}
    </p>
  );
}

function InsightIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="mb-0.5 inline-block h-4 w-4 text-info"
      aria-hidden
    >
      <path d="M9 18h6M10 21h4M12 3a6 6 0 0 0-3.5 10.9c.5.4.8 1 .8 1.6v.5h5.4v-.5c0-.6.3-1.2.8-1.6A6 6 0 0 0 12 3Z" />
    </svg>
  );
}

function ViewDetailsLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <Link
      href={href}
      className="inline-flex items-center gap-1 text-sm font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
    >
      {children}
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-3.5 w-3.5" aria-hidden>
        <path d="M9 6l6 6-6 6" />
      </svg>
    </Link>
  );
}

/** Two-category horizontal comparison bars - the anti-pattern doc's answer to
 * "a 2-slice pie is wrong": a bar reads two magnitudes cleanly, a donut with
 * only two slices does not. Local to this page since no other screen needs it. */
function ComparisonBars({
  rows,
}: {
  rows: readonly { label: string; value: number; tone: ChartTone }[];
}) {
  const max = Math.max(1, ...rows.map((row) => row.value));
  return (
    <div className="flex flex-col gap-3">
      {rows.map((row) => (
        <div key={row.label} className="flex items-center gap-3">
          <span className="w-36 shrink-0 truncate text-sm text-fg-muted">{row.label}</span>
          <div className="h-3 min-w-0 flex-1 overflow-hidden rounded-full bg-surface-3">
            <div
              className="h-full rounded-full transition-all duration-fast ease-out"
              style={{ width: `${(row.value / max) * 100}%`, backgroundColor: CHART_TONES[row.tone] }}
            />
          </div>
          <span className="nums w-10 shrink-0 text-right text-sm font-semibold text-fg">{row.value}</span>
        </div>
      ))}
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Bookings                                                                   */
/* -------------------------------------------------------------------------- */

type BookingBucket = "unassigned" | "assigned" | "inprogress" | "completed" | "other";

function bucketForStatus(status: BookingStatus): BookingBucket {
  switch (status) {
    case BookingStatus.Confirmed:
    case BookingStatus.AwaitingFulfilment:
      return "unassigned";
    case BookingStatus.Assigned:
      return "assigned";
    case BookingStatus.ProviderEnRoute:
    case BookingStatus.ProviderArrived:
    case BookingStatus.InProgress:
      return "inprogress";
    case BookingStatus.Completed:
      return "completed";
    default:
      return "other";
  }
}

const BUCKET_LABELS: Record<BookingBucket, string> = {
  unassigned: "Unassigned",
  assigned: "Assigned",
  inprogress: "In progress",
  completed: "Completed",
  other: "Other",
};

// Fixed categorical order, per the dataviz skill's color formula - never
// reassigned by value/rank, so a slice keeps its color as the day's counts change.
const BUCKET_TONES: Record<BookingBucket, ChartTone> = {
  unassigned: "warning",
  assigned: "info",
  inprogress: "brand",
  completed: "success",
  other: "accent",
};

/**
 * Bookings module: today's fulfilment board (`GET /bookings/fulfilment-board`
 * - the same endpoint `/fulfilment` renders as a kanban board), bucketed into
 * the board's own live-state groups. The "overdue unassigned" count is
 * computed here from real slot times, the same rule `/fulfilment` uses for
 * its own "Overdue / At risk" column (slot start already passed, no provider
 * assigned yet) - simplified to a single always-true "already overdue" test
 * rather than that page's additional "still comfortably ahead of the slot"
 * threshold, since this card only needs the number, not a live countdown.
 */
function BookingsSection() {
  const today = todayIsoDate();
  const query = useQuery({
    queryKey: ["overview-bookings", today] as const,
    queryFn: () => getFulfilmentBoard(today),
  });

  const items = query.data?.items ?? [];
  const now = new Date();
  const overdueUnassignedCount = items.filter((booking) => {
    const bucket = bucketForStatus(booking.status);
    if (bucket !== "unassigned") return false;
    const slotStart = new Date(`${booking.slotDate}T${booking.slotStartTime}`);
    return !Number.isNaN(slotStart.getTime()) && slotStart.getTime() < now.getTime();
  }).length;

  const buckets = new Map<BookingBucket, number>();
  for (const booking of items) {
    const bucket = bucketForStatus(booking.status);
    buckets.set(bucket, (buckets.get(bucket) ?? 0) + 1);
  }
  const donutData = (Object.keys(BUCKET_LABELS) as BookingBucket[])
    .filter((bucket) => (buckets.get(bucket) ?? 0) > 0)
    .map((bucket) => ({ label: BUCKET_LABELS[bucket], value: buckets.get(bucket) ?? 0, tone: BUCKET_TONES[bucket] }));

  return (
    <Card title="Bookings" description="Today's live fulfilment board.">
      {query.isPending ? (
        <SectionSkeleton />
      ) : query.isError ? (
        <SectionError error={query.error} onRetry={() => query.refetch()} />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-4">
            <KpiCard icon={<CalendarIcon />} tone="brand" label="Bookings today" value={items.length.toLocaleString("en-IN")} />
            <KpiCard icon={<AlertIcon />} tone="danger" label="Overdue unassigned" value={overdueUnassignedCount.toLocaleString("en-IN")} />
          </div>
          {items.length === 0 ? (
            <EmptyState title="No bookings today" description="Nothing on today's fulfilment board yet." />
          ) : (
            <DonutChart data={donutData} interactive />
          )}
          <Insight>
            {overdueUnassignedCount > 0
              ? `${overdueUnassignedCount} unassigned booking${overdueUnassignedCount === 1 ? " is" : "s are"} already past its slot start time and still has no provider.`
              : "No unassigned bookings are overdue against their slot right now."}
          </Insight>
          <ViewDetailsLink href="/bookings">View all bookings</ViewDetailsLink>
        </div>
      )}
    </Card>
  );
}

/* -------------------------------------------------------------------------- */
/* Payments                                                                   */
/* -------------------------------------------------------------------------- */

const STUCK_THRESHOLD_MINUTES = 24 * 60;

/**
 * Payments module: `GET /payments/reconciliation` (docs/OPEN-FIXES-FEATURES.csv
 * "Payment reconciliation") - the same stuck-pending/failed/orphaned queue
 * `/payments/reconciliation` lists in full. `stuckPendingCount`/`failedCount`/
 * `orphanedCount` on the response are the queue's real totals; the "stuck
 * over 24h" figure is computed from the fetched page's own `ageMinutes`
 * field (the queue is returned oldest-first, so a 100-row page captures every
 * stuck item unless the raw queue itself exceeds 100).
 */
function PaymentsSection() {
  const query = useQuery({
    queryKey: ["overview-payments"] as const,
    queryFn: () => getPaymentReconciliation(1, 100),
  });

  const data = query.data;
  const stuckOver24h =
    data?.items.filter(
      (item) => item.category === PaymentReconciliationCategory.StuckPending && item.ageMinutes > STUCK_THRESHOLD_MINUTES,
    ).length ?? 0;
  const totalIssues = (data?.stuckPendingCount ?? 0) + (data?.failedCount ?? 0) + (data?.orphanedCount ?? 0);

  const donutData = data
    ? [
        { label: "Stuck pending", value: data.stuckPendingCount, tone: "warning" as ChartTone },
        { label: "Failed", value: data.failedCount, tone: "danger" as ChartTone },
        { label: "Orphaned", value: data.orphanedCount, tone: "info" as ChartTone },
      ].filter((slice) => slice.value > 0)
    : [];

  return (
    <Card title="Payments" description="Stuck, failed and orphaned transactions awaiting reconciliation.">
      {query.isPending ? (
        <SectionSkeleton />
      ) : query.isError ? (
        <SectionError error={query.error} onRetry={() => query.refetch()} />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-4">
            <KpiCard icon={<CreditCardIcon />} tone="warning" label="Reconciliation issues" value={totalIssues.toLocaleString("en-IN")} />
            <KpiCard icon={<ClockIcon />} tone="danger" label="Stuck > 24h" value={stuckOver24h.toLocaleString("en-IN")} />
          </div>
          {totalIssues === 0 ? (
            <EmptyState title="Nothing to reconcile" description="No stuck, failed or orphaned payments right now." />
          ) : (
            <DonutChart data={donutData} interactive />
          )}
          <Insight>
            {stuckOver24h > 0
              ? `${stuckOver24h} payment${stuckOver24h === 1 ? " has" : "s have"} been stuck pending for over 24 hours.`
              : "No payments have been stuck pending for over 24 hours."}
          </Insight>
          <ViewDetailsLink href="/payments/reconciliation">View reconciliation queue</ViewDetailsLink>
        </div>
      )}
    </Card>
  );
}

/* -------------------------------------------------------------------------- */
/* Providers                                                                  */
/* -------------------------------------------------------------------------- */

/**
 * Providers module: `GET /providers/onboarding-overview?date=` - today's
 * registration cohort funnel, the same endpoint `/providers/onboarding`
 * renders as six tiles. The donut reads three onboarding-stage counts
 * (`ProviderOnboardingStatus`, a single field per provider, so these three
 * are mutually exclusive for a given provider - see that page's own doc
 * comment) as today's cohort distribution.
 */
function ProvidersSection() {
  const today = todayIsoDate();
  const query = useQuery({
    queryKey: ["overview-providers", today] as const,
    queryFn: () => getProviderOnboardingOverview(today),
  });

  const data = query.data;
  const cohort = data?.todayOnboardingCount ?? 0;
  const conversion = data && cohort > 0 ? Math.round((data.liveCount / cohort) * 100) : null;

  const donutData = data
    ? [
        { label: "Docs in review", value: data.documentVerificationCount, tone: "warning" as ChartTone },
        { label: "Verified", value: data.verifiedCount, tone: "info" as ChartTone },
        { label: "Live", value: data.liveCount, tone: "success" as ChartTone },
      ].filter((slice) => slice.value > 0)
    : [];

  return (
    <Card title="Providers" description="Today's onboarding cohort, by stage.">
      {query.isPending ? (
        <SectionSkeleton />
      ) : query.isError ? (
        <SectionError error={query.error} onRetry={() => query.refetch()} />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-4">
            <KpiCard icon={<ProviderIcon />} tone="brand" label="Today's onboarding" value={cohort.toLocaleString("en-IN")} />
            <KpiCard icon={<CheckCircleIcon />} tone="success" label="Live" value={(data?.liveCount ?? 0).toLocaleString("en-IN")} />
          </div>
          {cohort === 0 ? (
            <EmptyState title="No providers registered today" description="Nobody has started onboarding yet today." />
          ) : (
            <DonutChart data={donutData} interactive />
          )}
          <Insight>
            {conversion !== null
              ? `Provider onboarding conversion is ${conversion}% today (${data?.liveCount ?? 0} of ${cohort} registered providers are already live).`
              : "No providers have registered yet today."}
          </Insight>
          <ViewDetailsLink href="/providers/onboarding">View onboarding overview</ViewDetailsLink>
        </div>
      )}
    </Card>
  );
}

/* -------------------------------------------------------------------------- */
/* Customers                                                                  */
/* -------------------------------------------------------------------------- */

const CUSTOMER_TREND_DAYS = 30;

/**
 * Customers module: `GET /customers/analytics?trendDays=30` - the same
 * endpoint `/customers/analytics` uses for its own tiles and trend graph.
 * The area chart is that page's registration trend verbatim, rendered with
 * `interactive` so this flagship page gets the hover crosshair+tooltip
 * upgrade (`/customers/analytics` itself keeps rendering non-interactive,
 * unaffected by the new opt-in prop).
 */
function CustomersSection() {
  const query = useQuery({
    queryKey: ["overview-customers", CUSTOMER_TREND_DAYS] as const,
    queryFn: () =>
      apiFetch<CustomerAnalyticsResponse>(`${API_V1}/customers/analytics?trendDays=${CUSTOMER_TREND_DAYS}`, {
        authenticated: true,
      }),
  });

  const data = query.data;
  const activationRate = data && data.totalCustomers > 0 ? Math.round((data.customersWithBookings / data.totalCustomers) * 100) : null;

  const trendLabels = (data?.registrationTrend ?? []).map((point) =>
    new Date(`${point.date}T00:00:00Z`).toLocaleDateString("en-IN", { day: "numeric", month: "short", timeZone: "UTC" }),
  );
  const trendValues = (data?.registrationTrend ?? []).map((point) => point.count);

  return (
    <Card title="Customers" description={`Registrations over the last ${CUSTOMER_TREND_DAYS} days.`}>
      {query.isPending ? (
        <SectionSkeleton />
      ) : query.isError ? (
        <SectionError error={query.error} onRetry={() => query.refetch()} />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-4">
            <KpiCard icon={<UsersIcon />} tone="brand" label="Total customers" value={(data?.totalCustomers ?? 0).toLocaleString("en-IN")} />
            <KpiCard icon={<SparkleIcon />} tone="info" label="New today" value={(data?.newToday ?? 0).toLocaleString("en-IN")} />
          </div>
          {trendValues.every((value) => value === 0) ? (
            <EmptyState title="No new registrations" description={`Nobody registered in the last ${CUSTOMER_TREND_DAYS} days.`} />
          ) : (
            <AreaChart values={trendValues} labels={trendLabels} tone="brand" height={180} interactive />
          )}
          <Insight>
            {activationRate !== null
              ? `${activationRate}% of customers have completed at least one booking.`
              : "No customers registered yet."}
          </Insight>
          <ViewDetailsLink href="/customers/analytics">View customer analytics</ViewDetailsLink>
        </div>
      )}
    </Card>
  );
}

/* -------------------------------------------------------------------------- */
/* Catalog                                                                    */
/* -------------------------------------------------------------------------- */

// Matches CatalogHealthSection.tsx's own REASON_LABELS exactly (short form)
// rather than a separate, longer wording — consistency with the linked
// detail page, and short enough not to truncate in the donut legend's
// narrow column at mobile width (375px).
const HEALTH_REASON_LABELS: Record<CatalogHealthReason, string> = {
  NoPrice: "No price",
  NoMapping: "No mapping",
  NoImage: "No image",
  NeverBooked: "Never booked",
};

// Fixed categorical order - the two "blocks a sale outright" reasons lead.
const HEALTH_REASON_TONES: Record<CatalogHealthReason, ChartTone> = {
  NoPrice: "danger",
  NoMapping: "warning",
  NoImage: "info",
  NeverBooked: "accent",
};

/**
 * Catalog module: `GET /catalog/services/health` - every active service
 * failing a pre-publish completeness check, the same list `/catalog/health`
 * renders in full. The donut counts how often each reason occurs across all
 * flagged services (a service with two reasons counts toward both slices, so
 * this reads as "how common is each failure," not a strict partition of the
 * service count above it - the same non-partition caveat the Provider
 * Onboarding Overview page states in its own "How to read this" card).
 */
function CatalogSection() {
  const query = useQuery({
    queryKey: ["overview-catalog"] as const,
    queryFn: () => listCatalogHealthIssues(),
  });

  const issues = query.data ?? [];
  const missingPriceOrMapping = issues.filter(
    (issue) => issue.reasons.includes("NoPrice") || issue.reasons.includes("NoMapping"),
  ).length;

  const reasonCounts = new Map<CatalogHealthReason, number>();
  for (const issue of issues) {
    for (const reason of issue.reasons) {
      reasonCounts.set(reason, (reasonCounts.get(reason) ?? 0) + 1);
    }
  }
  const donutData = (Object.keys(HEALTH_REASON_LABELS) as CatalogHealthReason[])
    .filter((reason) => (reasonCounts.get(reason) ?? 0) > 0)
    .map((reason) => ({ label: HEALTH_REASON_LABELS[reason], value: reasonCounts.get(reason) ?? 0, tone: HEALTH_REASON_TONES[reason] }));

  return (
    <Card title="Catalog" description="Active services failing a pre-publish completeness check.">
      {query.isPending ? (
        <SectionSkeleton />
      ) : query.isError ? (
        <SectionError error={query.error} onRetry={() => query.refetch()} />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-4">
            <KpiCard icon={<PackageIcon />} tone="warning" label="Health issues" value={issues.length.toLocaleString("en-IN")} />
            <KpiCard icon={<TagIcon />} tone="danger" label="Missing price or mapping" value={missingPriceOrMapping.toLocaleString("en-IN")} />
          </div>
          {issues.length === 0 ? (
            <EmptyState title="Catalog is healthy" description="Every active service passes its completeness checks." />
          ) : (
            <DonutChart data={donutData} interactive />
          )}
          <Insight>
            {missingPriceOrMapping > 0
              ? `${missingPriceOrMapping} active service${missingPriceOrMapping === 1 ? " is" : "s are"} missing a price or a serviceability mapping.`
              : "No active services are missing a price or mapping."}
          </Insight>
          <ViewDetailsLink href="/catalog/health">View catalog health</ViewDetailsLink>
        </div>
      )}
    </Card>
  );
}

/* -------------------------------------------------------------------------- */
/* Serviceability                                                             */
/* -------------------------------------------------------------------------- */

/**
 * Serviceability module: `GET /serviceability-mappings/coverage-gaps` and
 * `/unmapped-active-services` - the same two lists `/serviceability/coverage-gaps`
 * renders. Two small counts, not a time series or a breakdown with more than
 * two categories, so per the dataviz skill's own anti-pattern guidance ("a
 * one-bar bar chart, or a 2-slice pie -> a stat tile, the number is the
 * chart") this section skips a donut/area chart in favor of a simple
 * two-row magnitude comparison instead of forcing an unsuited chart type.
 */
function ServiceabilitySection() {
  const gapsQuery = useQuery({
    queryKey: ["overview-serviceability-gaps"] as const,
    queryFn: () => listServiceabilityCoverageGaps(),
  });
  const unmappedQuery = useQuery({
    queryKey: ["overview-serviceability-unmapped"] as const,
    queryFn: () => listUnmappedActiveServices(),
  });

  const isPending = gapsQuery.isPending || unmappedQuery.isPending;
  const error = gapsQuery.error ?? unmappedQuery.error;

  const gaps = gapsQuery.data ?? [];
  const unmapped = unmappedQuery.data ?? [];

  return (
    <Card title="Serviceability" description="Where catalog coverage and provider coverage disagree.">
      {isPending ? (
        <SectionSkeleton />
      ) : error ? (
        <SectionError
          error={error}
          onRetry={() => {
            gapsQuery.refetch();
            unmappedQuery.refetch();
          }}
        />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-4">
            <KpiCard icon={<MapPinIcon />} tone="warning" label="Coverage gaps" value={gaps.length.toLocaleString("en-IN")} />
            <KpiCard icon={<AlertIcon />} tone="danger" label="Unmapped active services" value={unmapped.length.toLocaleString("en-IN")} />
          </div>
          {gaps.length === 0 && unmapped.length === 0 ? (
            <EmptyState title="Full coverage" description="No coverage gaps or unmapped active services found." />
          ) : (
            <ComparisonBars
              rows={[
                { label: "Unmapped services", value: unmapped.length, tone: "danger" },
                { label: "Coverage gaps", value: gaps.length, tone: "warning" },
              ]}
            />
          )}
          <Insight>
            {unmapped.length > 0 || gaps.length > 0
              ? `${unmapped.length} active service${unmapped.length === 1 ? " is" : "s are"} unbookable in every pincode, and ${gaps.length} pincode${gaps.length === 1 ? "" : "s"} already ${gaps.length === 1 ? "has" : "have"} provider coverage waiting on a mapping.`
              : "Every active service has at least one pincode mapping, and every mapping has provider coverage."}
          </Insight>
          <ViewDetailsLink href="/serviceability/coverage-gaps">View coverage gaps</ViewDetailsLink>
        </div>
      )}
    </Card>
  );
}

/* -------------------------------------------------------------------------- */
/* Icons                                                                      */
/* -------------------------------------------------------------------------- */

const ICON_PROPS = {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "1.75",
  strokeLinecap: "round" as const,
  strokeLinejoin: "round" as const,
  className: "h-5 w-5",
  "aria-hidden": true,
};

function CalendarIcon() {
  return (
    <svg {...ICON_PROPS}>
      <rect x="3.5" y="4.5" width="17" height="16" rx="2" />
      <path d="M3.5 9.5h17M8 2.5v4M16 2.5v4" />
    </svg>
  );
}

function AlertIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M12 3.5 21 19.5H3Z" />
      <path d="M12 9.5v4.5M12 17h.01" />
    </svg>
  );
}

function CreditCardIcon() {
  return (
    <svg {...ICON_PROPS}>
      <rect x="2.5" y="5.5" width="19" height="13" rx="2.25" />
      <path d="M2.5 10h19M6 15h4" />
    </svg>
  );
}

function ClockIcon() {
  return (
    <svg {...ICON_PROPS}>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 7.5V12l3 2" />
    </svg>
  );
}

function ProviderIcon() {
  return (
    <svg {...ICON_PROPS}>
      <rect x="3" y="7.5" width="18" height="12" rx="2" />
      <path d="M8.5 7.5v-2a2 2 0 0 1 2-2h3a2 2 0 0 1 2 2v2M3 12.5h18" />
    </svg>
  );
}

function CheckCircleIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M12 3.5 5 6.5v5.5c0 4.6 3 7.6 7 8.5 4-0.9 7-3.9 7-8.5V6.5L12 3.5Z" />
      <path d="m9 12 2 2 4-4.5" />
    </svg>
  );
}

function UsersIcon() {
  return (
    <svg {...ICON_PROPS}>
      <circle cx="9" cy="8" r="3.2" />
      <path d="M2.8 19.5a6.3 6.3 0 0 1 12.4 0" />
      <path d="M16 5.3a3.2 3.2 0 0 1 0 6.2M18.6 19.5a6.3 6.3 0 0 0-3.4-5.6" />
    </svg>
  );
}

function SparkleIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M12 3.5 13.6 9l5.4 1.5L13.6 12l-1.6 5.5L10.4 12 5 10.5 10.4 9Z" />
    </svg>
  );
}

function PackageIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M12 3.5 3.5 8 12 12.5 20.5 8Z" />
      <path d="m3.5 12 8.5 4.5L20.5 12M3.5 16l8.5 4.5L20.5 16" />
    </svg>
  );
}

function TagIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="m3.5 12.5 8-8H18v6.5l-8 8Z" />
      <circle cx="14" cy="7.5" r="1.3" fill="currentColor" stroke="none" />
    </svg>
  );
}

function MapPinIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M12 21s7-6.1 7-11.5A7 7 0 0 0 5 9.5C5 14.9 12 21 12 21Z" />
      <circle cx="12" cy="9.5" r="2.5" />
    </svg>
  );
}
