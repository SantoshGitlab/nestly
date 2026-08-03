"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Alert, Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import {
  downloadBlob,
  downloadExportJob,
  exportBookingRevenueCsv,
  exportCouponUsageCsv,
  exportCustomerSegmentationCsv,
  exportRefundCsv,
  exportSupportTicketCsv,
  getBookingRevenueReport,
  getCouponUsageReport,
  getCustomerSegmentationReport,
  getRefundReport,
  getSupportTicketReport,
  listMyExportJobs,
  requestExportJob,
} from "@/lib/reports-api";
import { ExportJobStatus, ExportReportType } from "@/lib/reports-types";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { isoDateOffsetFromToday, todayIsoDate } from "@/lib/date";

function useAdminClaims(): AdminSessionClaims | null {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);
  return claims;
}

/** `yyyy-mm-dd` -> a UTC day-start/day-end instant, same convention as lib/audit.ts's date filters. */
function toUtcStart(dateOnly: string): string {
  return `${dateOnly}T00:00:00.000Z`;
}

function toUtcEnd(dateOnly: string): string {
  return `${dateOnly}T23:59:59.999Z`;
}

function defaultFromDate(): string {
  return isoDateOffsetFromToday(-30);
}

function defaultToDate(): string {
  return todayIsoDate();
}

const REPORT_TYPE_OPTIONS = [
  { value: String(ExportReportType.BookingRevenue), label: "Booking & Revenue" },
  { value: String(ExportReportType.RefundUsage), label: "Refunds" },
  { value: String(ExportReportType.CouponUsage), label: "Coupon Usage" },
  { value: String(ExportReportType.CustomerSegmentation), label: "Customer Segmentation" },
  { value: String(ExportReportType.SupportTicket), label: "Support Tickets" },
];

function exportJobStatusLabel(status: ExportJobStatus): string {
  switch (status) {
    case ExportJobStatus.Pending:
      return "Pending";
    case ExportJobStatus.Processing:
      return "Processing";
    case ExportJobStatus.Completed:
      return "Completed";
    case ExportJobStatus.Failed:
      return "Failed";
    default:
      return "Unknown";
  }
}

/**
 * Reports and exports (SRS 12.18, tasks 128a-129): standard report screens
 * with a shared date-range filter and instant CSV export for 128a-c, plus a
 * permission-gated async export queue (128d) for large date ranges. Instant
 * export buttons are visible to any admin holding "reports.read" (mirrors
 * ReviewsController's CSV export, which needs only Read); the async export
 * request form is gated on "reports.write" since it creates a persisted job
 * and occupies a background worker slot.
 */
export default function ReportsPage() {
  const claims = useAdminClaims();
  const canRequestAsyncExport = canWriteModule(claims, "reports");
  const queryClient = useQueryClient();

  const [fromDate, setFromDate] = useState(defaultFromDate());
  const [toDate, setToDate] = useState(defaultToDate());
  const [city, setCity] = useState("");
  const [appliedRange, setAppliedRange] = useState({ fromDate: defaultFromDate(), toDate: defaultToDate(), city: "" });

  const onApplyRange = (e: FormEvent) => {
    e.preventDefault();
    setAppliedRange({ fromDate, toDate, city });
  };

  const dateRangeFilters = { fromUtc: toUtcStart(appliedRange.fromDate), toUtc: toUtcEnd(appliedRange.toDate) };
  const bookingRevenueFilters = { ...dateRangeFilters, city: appliedRange.city || undefined };

  const bookingRevenueQuery = useQuery({
    queryKey: ["reports", "booking-revenue", bookingRevenueFilters],
    queryFn: () => getBookingRevenueReport(bookingRevenueFilters),
  });
  const refundQuery = useQuery({
    queryKey: ["reports", "refunds", dateRangeFilters],
    queryFn: () => getRefundReport(dateRangeFilters),
  });
  const couponUsageQuery = useQuery({
    queryKey: ["reports", "coupon-usage", dateRangeFilters],
    queryFn: () => getCouponUsageReport(dateRangeFilters),
  });
  const customerSegmentationQuery = useQuery({
    queryKey: ["reports", "customer-segmentation", dateRangeFilters],
    queryFn: () =>
      getCustomerSegmentationReport({
        registeredFromUtc: dateRangeFilters.fromUtc,
        registeredToUtc: dateRangeFilters.toUtc,
      }),
  });
  const supportTicketQuery = useQuery({
    queryKey: ["reports", "support-tickets", dateRangeFilters],
    queryFn: () => getSupportTicketReport(dateRangeFilters),
  });

  const [exportError, setExportError] = useState<string | null>(null);

  async function runExport(exportFn: () => Promise<Blob>, fileNamePrefix: string) {
    setExportError(null);
    try {
      const blob = await exportFn();
      downloadBlob(blob, `${fileNamePrefix}-${todayIsoDate()}.csv`);
    } catch (err) {
      setExportError(describeError(err));
    }
  }

  // Async export queue (task 128d)
  const [asyncReportType, setAsyncReportType] = useState(String(ExportReportType.BookingRevenue));
  const [asyncRequestError, setAsyncRequestError] = useState<string | null>(null);

  const myExportsQuery = useQuery({
    queryKey: ["reports", "my-exports"],
    queryFn: listMyExportJobs,
    refetchInterval: (query) => {
      const jobs = query.state.data ?? [];
      const hasInFlight = jobs.some(
        (j) => j.status === ExportJobStatus.Pending || j.status === ExportJobStatus.Processing,
      );
      return hasInFlight ? 3000 : false;
    },
  });

  const requestExportMutation = useMutation({
    mutationFn: () =>
      requestExportJob({
        reportType: Number(asyncReportType) as ExportReportType,
        fromUtc: dateRangeFilters.fromUtc,
        toUtc: dateRangeFilters.toUtc,
        city: Number(asyncReportType) === ExportReportType.BookingRevenue ? appliedRange.city || null : null,
      }),
    onSuccess: () => {
      setAsyncRequestError(null);
      queryClient.invalidateQueries({ queryKey: ["reports", "my-exports"] });
    },
    onError: (err) => setAsyncRequestError(describeError(err)),
  });

  const onDownloadExportJob = async (jobId: string, reportType: ExportReportType) => {
    setExportError(null);
    try {
      const blob = await downloadExportJob(jobId);
      const label = REPORT_TYPE_OPTIONS.find((o) => Number(o.value) === reportType)?.label ?? "export";
      downloadBlob(blob, `${label.toLowerCase().replace(/\s+/g, "-")}-${jobId}.csv`);
    } catch (err) {
      setExportError(describeError(err));
    }
  };

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
      <PageHeading
        title="Reports & Exports"
        subtitle="Standard admin reports with filters and CSV export, plus an async export queue for large date ranges (SRS 12.18)."
      />

      {exportError ? <Alert>{exportError}</Alert> : null}

      <Card title="Date range" description="Applies to every report below (Customer Segmentation uses it as a registration window).">
        <form onSubmit={onApplyRange} className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="From" type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
          <Field label="To" type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
          <Field
            label="City (Booking & Revenue only)"
            value={city}
            onChange={(e) => setCity(e.target.value)}
            placeholder="Optional"
          />
          <div className="flex items-end">
            <Button type="submit">Apply</Button>
          </div>
        </form>
      </Card>

      <Card title="Booking & Revenue report" description="SRS 12.18.1, task 128a.">
        {bookingRevenueQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : bookingRevenueQuery.isError ? (
          <Alert>{describeError(bookingRevenueQuery.error)}</Alert>
        ) : (
          <>
            <dl className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
              <div>
                <dt className="text-neutral-500">Bookings</dt>
                <dd className="text-lg font-semibold">{bookingRevenueQuery.data.totalBookingsCount}</dd>
              </div>
              <div>
                <dt className="text-neutral-500">Revenue</dt>
                <dd className="text-lg font-semibold">₹{bookingRevenueQuery.data.totalRevenue.toFixed(2)}</dd>
              </div>
            </dl>
            <div className="mt-4">
              <Button
                variant="secondary"
                onClick={() => runExport(() => exportBookingRevenueCsv(bookingRevenueFilters), "booking-revenue")}
              >
                Export CSV
              </Button>
            </div>
          </>
        )}
      </Card>

      <Card title="Refund report" description="SRS 12.18.1, task 128b.">
        {refundQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : refundQuery.isError ? (
          <Alert>{describeError(refundQuery.error)}</Alert>
        ) : (
          <>
            <dl className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
              <div>
                <dt className="text-neutral-500">Refunds</dt>
                <dd className="text-lg font-semibold">{refundQuery.data.totalCount}</dd>
              </div>
              <div>
                <dt className="text-neutral-500">Total refunded</dt>
                <dd className="text-lg font-semibold">₹{refundQuery.data.totalRefundedAmount.toFixed(2)}</dd>
              </div>
            </dl>
            <div className="mt-4">
              <Button variant="secondary" onClick={() => runExport(() => exportRefundCsv(dateRangeFilters), "refunds")}>
                Export CSV
              </Button>
            </div>
          </>
        )}
      </Card>

      <Card title="Coupon usage report" description="SRS 12.18.1, task 128b.">
        {couponUsageQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : couponUsageQuery.isError ? (
          <Alert>{describeError(couponUsageQuery.error)}</Alert>
        ) : (
          <>
            <dl className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
              <div>
                <dt className="text-neutral-500">Redemptions</dt>
                <dd className="text-lg font-semibold">{couponUsageQuery.data.totalRedemptions}</dd>
              </div>
              <div>
                <dt className="text-neutral-500">Total discount</dt>
                <dd className="text-lg font-semibold">₹{couponUsageQuery.data.totalDiscountAmount.toFixed(2)}</dd>
              </div>
            </dl>
            {couponUsageQuery.data.rows.length > 0 ? (
              <ul className="mt-4 flex flex-col gap-2 text-sm">
                {couponUsageQuery.data.rows.map((row) => (
                  <li
                    key={row.couponId}
                    className="flex items-center justify-between rounded-lg border border-black/10 p-3 dark:border-white/15"
                  >
                    <span className="font-mono">{row.couponCode}</span>
                    <span>{row.redemptionCount} redemptions</span>
                    <span>₹{row.totalDiscountAmount.toFixed(2)}</span>
                  </li>
                ))}
              </ul>
            ) : null}
            <div className="mt-4">
              <Button variant="secondary" onClick={() => runExport(() => exportCouponUsageCsv(dateRangeFilters), "coupon-usage")}>
                Export CSV
              </Button>
            </div>
          </>
        )}
      </Card>

      <Card title="Customer segmentation report" description="SRS 12.18.1, task 128c.">
        {customerSegmentationQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : customerSegmentationQuery.isError ? (
          <Alert>{describeError(customerSegmentationQuery.error)}</Alert>
        ) : (
          <>
            <p className="text-sm text-neutral-600 dark:text-neutral-400">
              {customerSegmentationQuery.data.totalCustomers} customers registered in this window.
            </p>
            <div className="mt-4 grid grid-cols-1 gap-6 sm:grid-cols-2">
              <div>
                <h3 className="text-sm font-medium">By status</h3>
                <ul className="mt-2 flex flex-col gap-1 text-sm">
                  {customerSegmentationQuery.data.byStatus.map((row) => (
                    <li key={row.status} className="flex justify-between">
                      <span>{["Active", "Blocked", "Unverified", "Deleted"][row.status] ?? "Unknown"}</span>
                      <span>{row.count}</span>
                    </li>
                  ))}
                </ul>
              </div>
              <div>
                <h3 className="text-sm font-medium">By city</h3>
                <ul className="mt-2 flex flex-col gap-1 text-sm">
                  {customerSegmentationQuery.data.byCity.slice(0, 10).map((row) => (
                    <li key={row.city} className="flex justify-between">
                      <span>{row.city}</span>
                      <span>{row.count}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
            <div className="mt-4">
              <Button
                variant="secondary"
                onClick={() =>
                  runExport(
                    () =>
                      exportCustomerSegmentationCsv({
                        registeredFromUtc: dateRangeFilters.fromUtc,
                        registeredToUtc: dateRangeFilters.toUtc,
                      }),
                    "customer-segmentation",
                  )
                }
              >
                Export CSV
              </Button>
            </div>
          </>
        )}
      </Card>

      <Card title="Support ticket report" description="SRS 12.18.1, task 128c.">
        {supportTicketQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : supportTicketQuery.isError ? (
          <Alert>{describeError(supportTicketQuery.error)}</Alert>
        ) : (
          <>
            <dl className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-3">
              <div>
                <dt className="text-neutral-500">Total tickets</dt>
                <dd className="text-lg font-semibold">{supportTicketQuery.data.totalTickets}</dd>
              </div>
              <div>
                <dt className="text-neutral-500">Resolved/closed</dt>
                <dd className="text-lg font-semibold">{supportTicketQuery.data.resolvedCount}</dd>
              </div>
              <div>
                <dt className="text-neutral-500">Avg. resolution time</dt>
                <dd className="text-lg font-semibold">
                  {supportTicketQuery.data.averageResolutionHours === null
                    ? "—"
                    : `${supportTicketQuery.data.averageResolutionHours.toFixed(1)}h`}
                </dd>
              </div>
            </dl>
            <div className="mt-4">
              <Button
                variant="secondary"
                onClick={() => runExport(() => exportSupportTicketCsv(dateRangeFilters), "support-tickets")}
              >
                Export CSV
              </Button>
            </div>
          </>
        )}
      </Card>

      {canRequestAsyncExport ? (
        <Card
          title="Async export queue"
          description="For a large date range: a background job generates the file - poll or come back later to download it (SRS 12.18.2, task 128d)."
        >
          {asyncRequestError ? <Alert>{asyncRequestError}</Alert> : null}
          <div className="flex flex-col gap-4 sm:flex-row sm:items-end">
            <div className="flex-1">
              <Select
                label="Report"
                options={REPORT_TYPE_OPTIONS}
                value={asyncReportType}
                onChange={(e) => setAsyncReportType(e.target.value)}
              />
            </div>
            <Button disabled={requestExportMutation.isPending} onClick={() => requestExportMutation.mutate()}>
              {requestExportMutation.isPending ? "Requesting…" : "Request export"}
            </Button>
          </div>

          <div className="mt-6 border-t border-black/10 pt-5 dark:border-white/15">
            <h3 className="text-sm font-medium">My exports</h3>
            {myExportsQuery.isPending ? (
              <p className="mt-2 text-sm text-neutral-500">Loading…</p>
            ) : myExportsQuery.isError ? (
              <Alert>{describeError(myExportsQuery.error)}</Alert>
            ) : myExportsQuery.data.length === 0 ? (
              <p className="mt-2 text-sm text-neutral-500">No exports requested yet.</p>
            ) : (
              <ul className="mt-2 flex flex-col gap-2 text-sm">
                {myExportsQuery.data.map((job) => (
                  <li
                    key={job.id}
                    className="flex items-center justify-between rounded-lg border border-black/10 p-3 dark:border-white/15"
                  >
                    <span>{REPORT_TYPE_OPTIONS.find((o) => Number(o.value) === job.reportType)?.label ?? "Report"}</span>
                    <span>{exportJobStatusLabel(job.status)}</span>
                    <span>{new Date(job.requestedAtUtc).toLocaleString()}</span>
                    {job.status === ExportJobStatus.Completed ? (
                      <Button variant="secondary" onClick={() => onDownloadExportJob(job.id, job.reportType)}>
                        Download
                      </Button>
                    ) : job.status === ExportJobStatus.Failed ? (
                      <span className="text-red-600 dark:text-red-400">{job.errorMessage ?? "Failed"}</span>
                    ) : null}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </Card>
      ) : null}
    </div>
  );
}
