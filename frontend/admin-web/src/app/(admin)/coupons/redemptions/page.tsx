"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { Badge, Button, Card, Field, PageHeading, Skeleton, StatTile } from "@/components/ui";
import { DataTable, formatCurrency } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { getCouponRedemptionReport } from "@/lib/coupon-api";
import type { CouponRedemptionReportRow } from "@/lib/coupon-types";
import { endOfLocalDayUtc, startOfLocalDayUtc } from "@/lib/day-range";
import { CouponsTabs } from "../_components/CouponsTabs";

/**
 * Redemptions per unique customer. Returns `null` rather than a number when
 * there is no customer to divide by — the previous version divided straight
 * through and rendered a literal "Infinity" (or "NaN") in the column for any
 * coupon the report returned with a zero customer count.
 */
function redemptionsPerCustomer(row: CouponRedemptionReportRow): number | null {
  return row.uniqueCustomerCount > 0 ? row.redemptionCount / row.uniqueCustomerCount : null;
}

/**
 * Coupon redemption reporting (SRS 12.12.2, task 119): redemptions, discount
 * total, booking count (one redemption is one booking - see
 * CouponRedemptionReportRow's backend doc comment), and a per-coupon
 * unique-customer count as the closest available abuse/concentration signal.
 * `couponId` arrives as a query param when navigated to from a specific
 * coupon's "Report" link on the manage screen (SRS 12.12.1); omitted, this
 * reports across every coupon with redemptions in the selected window.
 *
 * Wrapped in Suspense: useSearchParams opts the tree below it out of static
 * rendering, and Next's App Router requires a Suspense boundary around that
 * or the production build fails (same pattern customer-web/src/app/search
 * uses).
 */
export default function CouponRedemptionReportPage() {
  return (
    <Suspense fallback={<div className="mx-auto w-full max-w-6xl px-6 py-10" />}>
      <CouponRedemptionReport />
    </Suspense>
  );
}

function CouponRedemptionReport() {
  const router = useRouter();
  const searchParams = useSearchParams();

  const couponId = searchParams.get("couponId") ?? undefined;
  const fromDate = searchParams.get("fromDate") ?? "";
  const toDate = searchParams.get("toDate") ?? "";

  const reportQuery = useQuery({
    queryKey: ["coupons", "redemption-report", couponId, fromDate, toDate] as const,
    queryFn: () =>
      getCouponRedemptionReport({
        couponId,
        // Local day boundaries, not `${date}T00:00:00Z` — see lib/day-range.
        fromUtc: fromDate ? (startOfLocalDayUtc(fromDate) ?? undefined) : undefined,
        toUtc: toDate ? (endOfLocalDayUtc(toDate) ?? undefined) : undefined,
      }),
  });

  function updateParam(key: string, value: string) {
    const next = new URLSearchParams(searchParams.toString());
    if (value) {
      next.set(key, value);
    } else {
      next.delete(key);
    }
    // `replace`, not `push`: a date control fires on every component change,
    // so pushing buried the previous screen under a dozen history entries.
    router.replace(`/coupons/redemptions${next.toString() ? `?${next.toString()}` : ""}`);
  }

  const columns: DataTableColumn<CouponRedemptionReportRow>[] = [
    {
      key: "code",
      header: "Code",
      sortValue: (row) => row.code,
      cell: (row) => <span className="nums font-medium text-fg">{row.code}</span>,
    },
    {
      key: "status",
      header: "Status",
      sortValue: (row) => row.isActive,
      cell: (row) => (
        <Badge tone={row.isActive ? "success" : "neutral"}>{row.isActive ? "Active" : "Suspended"}</Badge>
      ),
    },
    {
      key: "redemptions",
      header: "Redemptions",
      numeric: true,
      sortValue: (row) => row.redemptionCount,
      cell: (row) => row.redemptionCount.toLocaleString("en-IN"),
    },
    {
      key: "discount",
      header: "Discount total",
      numeric: true,
      sortValue: (row) => row.totalDiscountAmount,
      cell: (row) => formatCurrency(row.totalDiscountAmount),
    },
    {
      key: "customers",
      header: "Unique customers",
      numeric: true,
      sortValue: (row) => row.uniqueCustomerCount,
      cell: (row) => row.uniqueCustomerCount.toLocaleString("en-IN"),
    },
    {
      key: "concentration",
      header: "Redemptions / customer",
      numeric: true,
      sortValue: (row) => redemptionsPerCustomer(row),
      cell: (row) => {
        const ratio = redemptionsPerCustomer(row);
        if (ratio === null) return <span className="text-fg-subtle">—</span>;
        return (
          <span className="inline-flex items-center justify-end gap-2">
            {ratio.toFixed(2)}
            {ratio > 1.5 ? (
              // The threshold is a heuristic, so the reason it fired travels
              // with it rather than sitting in a legend elsewhere on the page.
              <span title="Usage is concentrated in relatively few customers - the closest abuse signal this report can surface (SRS 12.12.2).">
                <Badge tone="warning" className="whitespace-nowrap">
                  Watch
                </Badge>
              </span>
            ) : null}
          </span>
        );
      },
    },
  ];

  const hasTotals = reportQuery.data !== undefined;

  return (
    <div className="mx-auto w-full max-w-6xl">
      <PageHeading
        title="Coupons & Campaigns"
        subtitle="Redemption reporting: redemptions, discount total, booking count, and usage concentration (SRS 12.12.2)."
      />

      <CouponsTabs />

      <div className="flex flex-col gap-6">
        <Card title="Filters" description="Scope the report to a date range and, optionally, a single coupon.">
          <div className="flex flex-wrap items-end gap-4">
            <div className="w-44">
              <Field
                label="From"
                type="date"
                max={toDate || undefined}
                value={fromDate}
                onChange={(event) => updateParam("fromDate", event.target.value)}
              />
            </div>
            <div className="w-44">
              <Field
                label="To"
                type="date"
                min={fromDate || undefined}
                value={toDate}
                onChange={(event) => updateParam("toDate", event.target.value)}
              />
            </div>
            {couponId ? (
              <Button type="button" variant="secondary" onClick={() => updateParam("couponId", "")}>
                Show every coupon
              </Button>
            ) : null}
          </div>
        </Card>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {/* While the first load is in flight there is no total yet, and a
              tile reading "0" is a wrong number stated as fact. */}
          {hasTotals ? (
            <>
              <StatTile
                label="Total redemptions"
                value={reportQuery.data.totalRedemptions.toLocaleString("en-IN")}
              />
              <StatTile
                label="Total discount given"
                value={formatCurrency(reportQuery.data.totalDiscountAmount)}
                title={formatCurrency(reportQuery.data.totalDiscountAmount)}
              />
            </>
          ) : (
            <>
              <StatTileSkeleton />
              <StatTileSkeleton />
            </>
          )}
        </div>

        <DataTable
          title="Per-coupon breakdown"
          description="Booking count using a coupon equals its redemption count - CouponRedemption links one booking to one coupon."
          columns={columns}
          rows={reportQuery.data?.rows}
          rowKey={(row) => row.couponId}
          isLoading={reportQuery.isPending}
          isFetching={reportQuery.isFetching}
          error={reportQuery.error}
          onRetry={() => reportQuery.refetch()}
          defaultSort={{ key: "redemptions", direction: "desc" }}
          caption="Redemptions per coupon in the selected window"
          emptyTitle="No redemptions in the selected window"
          emptyDescription="Widen the date range, or clear the coupon filter."
          skeletonRows={5}
          minWidth="900px"
        />
      </div>
    </div>
  );
}

/** Matches a `StatTile`'s height so the tiles do not jump when the totals land. */
function StatTileSkeleton() {
  return (
    <div className="rounded-2xl border border-line bg-surface p-5 shadow-sm">
      <Skeleton className="h-4 w-32" />
      <Skeleton className="mt-3 h-9 w-24" />
    </div>
  );
}
