"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { Card, Field, PageHeading, StatTile } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getCouponRedemptionReport } from "@/lib/coupon-api";
import { CouponsTabs } from "../_components/CouponsTabs";

/** `<input type="date">` yields "yyyy-mm-dd" - pinned to the start/end of that day in UTC, same convention CouponForm and lib/audit.ts use. */
function dateInputToStartOfDayUtc(date: string): string {
  return `${date}T00:00:00.000Z`;
}

function dateInputToEndOfDayUtc(date: string): string {
  return `${date}T23:59:59.999Z`;
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
        fromUtc: fromDate ? dateInputToStartOfDayUtc(fromDate) : undefined,
        toUtc: toDate ? dateInputToEndOfDayUtc(toDate) : undefined,
      }),
  });

  function updateParam(key: string, value: string) {
    const next = new URLSearchParams(searchParams.toString());
    if (value) {
      next.set(key, value);
    } else {
      next.delete(key);
    }
    router.push(`/coupons/redemptions${next.toString() ? `?${next.toString()}` : ""}`);
  }

  return (
    <div className="mx-auto w-full max-w-6xl">
      <PageHeading
        title="Coupons & Campaigns"
        subtitle="Redemption reporting: redemptions, discount total, booking count, and usage concentration (SRS 12.12.2)."
      />

      <CouponsTabs />

      <div className="flex flex-col gap-6">
        <Card title="Filters" description="Scope the report to a date range and, optionally, a single coupon.">
          <div className="flex flex-wrap items-end gap-3">
            <div className="w-44">
              <Field
                label="From"
                type="date"
                value={fromDate}
                onChange={(event) => updateParam("fromDate", event.target.value)}
              />
            </div>
            <div className="w-44">
              <Field
                label="To"
                type="date"
                value={toDate}
                onChange={(event) => updateParam("toDate", event.target.value)}
              />
            </div>
            {couponId ? (
              <button
                type="button"
                onClick={() => updateParam("couponId", "")}
                className="text-sm font-medium text-neutral-600 underline hover:text-black dark:text-neutral-400 dark:hover:text-white"
              >
                Clear coupon filter
              </button>
            ) : null}
          </div>
        </Card>

        {reportQuery.error ? (
          <Card title="Redemption report">
            <p className="text-sm text-red-600 dark:text-red-400">{describeError(reportQuery.error)}</p>
          </Card>
        ) : (
          <>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <StatTile label="Total redemptions" value={(reportQuery.data?.totalRedemptions ?? 0).toLocaleString("en-IN")} />
              <StatTile
                label="Total discount given"
                value={`₹${(reportQuery.data?.totalDiscountAmount ?? 0).toFixed(2)}`}
                title={`₹${(reportQuery.data?.totalDiscountAmount ?? 0).toFixed(2)}`}
              />
            </div>

            <Card
              title="Per-coupon breakdown"
              description="Booking count using a coupon equals its redemption count - CouponRedemption links one booking to one coupon."
            >
              {reportQuery.isLoading ? (
                <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>
              ) : !reportQuery.data || reportQuery.data.rows.length === 0 ? (
                <p className="text-sm text-neutral-600 dark:text-neutral-400">No redemptions in the selected window.</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full min-w-full text-left text-sm">
                    <thead>
                      <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
                        <th scope="col" className="px-3 py-2 font-medium">Code</th>
                        <th scope="col" className="px-3 py-2 font-medium">Status</th>
                        <th scope="col" className="px-3 py-2 font-medium">Redemptions</th>
                        <th scope="col" className="px-3 py-2 font-medium">Discount total</th>
                        <th scope="col" className="px-3 py-2 font-medium">Unique customers</th>
                        <th scope="col" className="px-3 py-2 font-medium">Avg. redemptions / customer</th>
                      </tr>
                    </thead>
                    <tbody>
                      {reportQuery.data.rows.map((row) => (
                        <tr key={row.couponId} className="border-b border-black/5 last:border-0 dark:border-white/10">
                          <td className="px-3 py-2 font-medium">{row.code}</td>
                          <td className="px-3 py-2">{row.isActive ? "Active" : "Suspended"}</td>
                          <td className="px-3 py-2">{row.redemptionCount}</td>
                          <td className="px-3 py-2">₹{row.totalDiscountAmount.toFixed(2)}</td>
                          <td className="px-3 py-2">{row.uniqueCustomerCount}</td>
                          <td className="px-3 py-2">
                            {(row.redemptionCount / row.uniqueCustomerCount).toFixed(2)}
                            {row.redemptionCount / row.uniqueCustomerCount > 1.5 ? (
                              <span
                                title="Usage is concentrated in relatively few customers - the closest abuse signal this report can surface (SRS 12.12.2)."
                                className="ml-2 inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-900 dark:bg-amber-950 dark:text-amber-200"
                              >
                                Watch
                              </span>
                            ) : null}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </Card>
          </>
        )}
      </div>
    </div>
  );
}
