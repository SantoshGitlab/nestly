"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useMemo, useState } from "react";
import { Alert, AreaChart, Button, Card, KpiCard, PageHeading, Select, Skeleton } from "@/components/ui";
import type { ChartTone } from "@/components/ui";
import { DataTable } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { CustomerStatus } from "@/lib/types";
import type { CustomerAnalyticsResponse, CustomerCityBreakdown } from "@/lib/types";
import { CustomersTabs } from "../_components/CustomersTabs";

const PERIOD_OPTIONS = [
  { value: "7", label: "Last 7 days" },
  { value: "30", label: "Last 30 days" },
  { value: "90", label: "Last 90 days" },
];

/**
 * Customer Analytics dashboard (Admin Web new page, per the admin's request:
 * "an admin page showing summarized customer details - counts and a graph,
 * similar to the provider overview"). The customer counterpart to the
 * Provider Onboarding Overview dashboard, built on real `Customer`/`Booking`
 * data only (backend/shared/Application/Customers/CustomerManagementContracts.cs'
 * `CustomerAnalyticsResponse` doc comment lists exactly what each count means
 * and what was deliberately left out - no KYC/verification concept, since
 * customers have no onboarding funnel the way providers do, and no wallet/
 * referral rollup).
 *
 * Backend: one endpoint, `GET /admin/customers/analytics?trendDays=` - a
 * small, bounded set of group-by/count queries, not one row per customer.
 *
 * Every status-breakdown tile is a `Link` to `/customers`, pre-filtered to
 * that exact status via the directory's existing `status` query param (same
 * click-through pattern as the Provider Onboarding Overview's tiles). The
 * three registration-cohort tiles (today/7-day/trend-window) compute the
 * exact same UTC calendar-day boundary the backend used - see
 * `windowStartIso` below - so a tile lands on precisely the cohort it
 * summarized; the funnel tiles use the directory's existing (if
 * currently form-hidden) `minBookingCount`/`maxBookingCount` filters.
 */
export default function CustomerAnalyticsPage() {
  const [trendDays, setTrendDays] = useState(30);

  const query = useQuery({
    queryKey: ["admin-customer-analytics", trendDays] as const,
    queryFn: () =>
      apiFetch<CustomerAnalyticsResponse>(`${API_V1}/customers/analytics?trendDays=${trendDays}`, {
        authenticated: true,
      }),
  });

  // Today's UTC calendar-day start, exactly as the backend computes it
  // (DateTime.SpecifyKind(DateTime.UtcNow.Date, Utc) in
  // CustomerRepository.GetAnalyticsCountsAsync) - not the local-day-in-UTC
  // conversion day-range.ts's helpers produce for other dashboards, since
  // this endpoint's cohorts are UTC-calendar-day windows, not local ones.
  const todayStartUtc = useMemo(() => {
    const now = new Date();
    return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
  }, []);

  function windowStartIso(daysAgo: number): string {
    const start = new Date(todayStartUtc);
    start.setUTCDate(start.getUTCDate() - daysAgo);
    return start.toISOString();
  }

  function tileHref(params: Record<string, string>): string {
    return `/customers?${new URLSearchParams(params).toString()}`;
  }

  const statusTiles: {
    key: string;
    label: string;
    tone: ChartTone;
    value: number | undefined;
    href: string;
    hint: string;
  }[] = [
    {
      key: "total",
      label: "Total customers",
      tone: "brand",
      value: query.data?.totalCustomers,
      href: "/customers",
      hint: "Every customer record, any status.",
    },
    {
      key: "active",
      label: "Active",
      tone: "success",
      value: query.data?.activeCount,
      href: tileHref({ status: String(CustomerStatus.Active) }),
      hint: "Can sign in and book.",
    },
    {
      key: "blocked",
      label: "Blocked",
      tone: "danger",
      value: query.data?.blockedCount,
      href: tileHref({ status: String(CustomerStatus.Blocked) }),
      hint: "Account blocked by an admin.",
    },
    {
      key: "unverified",
      label: "Unverified",
      tone: "warning",
      value: query.data?.unverifiedCount,
      href: tileHref({ status: String(CustomerStatus.Unverified) }),
      hint: "Registered, not yet verified.",
    },
    {
      key: "deleted",
      label: "Deleted",
      tone: "accent",
      value: query.data?.softDeletedCount,
      href: tileHref({ status: String(CustomerStatus.SoftDeleted) }),
      hint: "Right-to-erasure account deletions.",
    },
  ];

  const cohortTiles: {
    key: string;
    label: string;
    tone: ChartTone;
    value: number | undefined;
    href: string;
    hint: string;
  }[] = [
    {
      key: "newToday",
      label: "New today",
      tone: "brand",
      value: query.data?.newToday,
      href: tileHref({ registeredFromUtc: todayStartUtc.toISOString() }),
      hint: "Registered on today's UTC calendar date.",
    },
    {
      key: "new7",
      label: "New last 7 days",
      tone: "info",
      value: query.data?.newLast7Days,
      href: tileHref({ registeredFromUtc: windowStartIso(6) }),
      hint: "Trailing 7 days, today inclusive.",
    },
    {
      key: "newWindow",
      label: `New last ${trendDays} days`,
      tone: "accent",
      value: query.data?.newInTrendWindow,
      href: tileHref({ registeredFromUtc: windowStartIso(trendDays - 1) }),
      hint: "Same window as the trend graph below.",
    },
    {
      key: "withBookings",
      label: "With bookings",
      tone: "success",
      value: query.data?.customersWithBookings,
      href: tileHref({ minBookingCount: "1" }),
      hint: "Booked at least once - the activation half of the funnel.",
    },
    {
      key: "zeroBookings",
      label: "Zero bookings",
      tone: "warning",
      value: query.data?.customersWithZeroBookings,
      href: tileHref({ maxBookingCount: "0" }),
      hint: "Registered but never booked.",
    },
  ];

  const trendLabels = (query.data?.registrationTrend ?? []).map((point) =>
    new Date(`${point.date}T00:00:00Z`).toLocaleDateString("en-IN", { day: "numeric", month: "short", timeZone: "UTC" }),
  );
  const trendValues = (query.data?.registrationTrend ?? []).map((point) => point.count);

  const cityColumns: DataTableColumn<CustomerCityBreakdown>[] = [
    { key: "city", header: "City", cell: (row) => row.city },
    { key: "count", header: "Customers", numeric: true, cell: (row) => row.count.toLocaleString("en-IN") },
  ];

  return (
    <div className="w-full max-w-6xl">
      <PageHeading
        title="Customer Analytics"
        subtitle="Registrations, account status and booking activation across the customer base."
      />
      <CustomersTabs />

      <div className="mb-6 mt-6 flex flex-wrap items-end justify-between gap-4">
        <Select
          label="Trend window"
          value={String(trendDays)}
          onChange={(event) => setTrendDays(Number(event.target.value))}
          options={PERIOD_OPTIONS}
          className="w-44"
        />
        <Button variant="secondary" onClick={() => query.refetch()} loading={query.isFetching}>
          Refresh
        </Button>
      </div>

      {query.isError ? (
        <Alert tone="error" action={<Button size="sm" onClick={() => query.refetch()}>Retry</Button>}>
          {describeError(query.error)}
        </Alert>
      ) : null}

      {query.isPending ? (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {Array.from({ length: 5 }, (_, index) => (
            <div key={index} className="rounded-2xl bg-surface p-6 shadow-sm">
              <Skeleton className="h-4 w-24" />
              <Skeleton className="mt-3 h-8 w-16" />
            </div>
          ))}
        </div>
      ) : query.data ? (
        <>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
            {statusTiles.map((tile) => (
              <Link key={tile.key} href={tile.href} className="block" title={tile.hint}>
                <KpiCard tone={tile.tone} icon={<TileIcon tileKey={tile.key} />} label={tile.label} value={(tile.value ?? 0).toLocaleString("en-IN")} />
              </Link>
            ))}
          </div>

          <div className="mt-4 grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
            {cohortTiles.map((tile) => (
              <Link key={tile.key} href={tile.href} className="block" title={tile.hint}>
                <KpiCard tone={tile.tone} icon={<TileIcon tileKey={tile.key} />} label={tile.label} value={(tile.value ?? 0).toLocaleString("en-IN")} />
              </Link>
            ))}
          </div>

          <Card
            className="mt-6"
            title="New registrations"
            description={`Daily registration count for the last ${query.data.trendDays} days.`}
          >
            <AreaChart values={trendValues} labels={trendLabels} tone="brand" />
          </Card>

          <Card className="mt-6" title="Top cities" description="Cities with the most registered customers (blank/unknown city excluded).">
            <DataTable
              columns={cityColumns}
              rows={query.data.topCities}
              rowKey={(row) => row.city}
              isLoading={false}
              caption="Top cities by customer count"
              emptyTitle="No city data yet"
              emptyDescription="No customer has a city on their profile yet."
            />
          </Card>

          <Card className="mt-6" title="How to read this">
            <p className="text-sm text-fg-muted">
              The account-status tiles (<span className="font-medium text-fg">Active/Blocked/Unverified/Deleted</span>)
              partition every customer exactly once. The registration-cohort and booking-funnel tiles below them do
              not: <span className="font-medium text-fg">With bookings</span> and{" "}
              <span className="font-medium text-fg">Zero bookings</span> together add up to{" "}
              <span className="font-medium text-fg">Total customers</span>, but{" "}
              <span className="font-medium text-fg">New today</span> is a subset of{" "}
              <span className="font-medium text-fg">New last 7 days</span>, which is in turn a subset of{" "}
              <span className="font-medium text-fg">New last {query.data.trendDays} days</span> - the same window the
              graph above plots.
            </p>
          </Card>
        </>
      ) : null}
    </div>
  );
}

function TileIcon({ tileKey }: { tileKey: string }) {
  const common = {
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: "1.75",
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    className: "h-5 w-5",
    "aria-hidden": true,
  };

  switch (tileKey) {
    case "active":
      return (
        <svg {...common}>
          <path d="M12 3.5c4.7 0 8.5 3.8 8.5 8.5s-3.8 8.5-8.5 8.5-8.5-3.8-8.5-8.5" />
          <path d="m4.5 6.5 3 3" />
        </svg>
      );
    case "blocked":
      return (
        <svg {...common}>
          <circle cx="12" cy="12" r="8.5" />
          <path d="m6.5 6.5 11 11" />
        </svg>
      );
    case "unverified":
      return (
        <svg {...common}>
          <circle cx="12" cy="12" r="8.5" />
          <path d="M12 7.5V12l3 2" />
        </svg>
      );
    case "deleted":
      return (
        <svg {...common}>
          <path d="M5 7h14M9 7V5.5A1.5 1.5 0 0 1 10.5 4h3A1.5 1.5 0 0 1 15 5.5V7m-7 0 .7 12.1A1.5 1.5 0 0 0 10.2 20.5h3.6a1.5 1.5 0 0 0 1.5-1.4L16 7" />
        </svg>
      );
    case "newToday":
    case "new7":
    case "newWindow":
      return (
        <svg {...common}>
          <rect x="4" y="5" width="16" height="15" rx="1.5" />
          <path d="M4 9.5h16M8 3.5v3M16 3.5v3" />
        </svg>
      );
    case "withBookings":
      return (
        <svg {...common}>
          <path d="M12 3.5 5 6.5v5.5c0 4.6 3 7.6 7 8.5 4-0.9 7-3.9 7-8.5V6.5L12 3.5Z" />
          <path d="m9 12 2 2 4-4.5" />
        </svg>
      );
    case "zeroBookings":
      return (
        <svg {...common}>
          <path d="M12 3.5 5 6.5v5.5c0 4.6 3 7.6 7 8.5 4-0.9 7-3.9 7-8.5V6.5L12 3.5Z" />
          <path d="M9.5 9.5 14.5 14.5M14.5 9.5 9.5 14.5" />
        </svg>
      );
    default:
      return (
        <svg {...common}>
          <path d="M4 20V10.5l8-6.5 8 6.5V20" />
          <path d="M9.5 20v-6h5v6" />
        </svg>
      );
  }
}
