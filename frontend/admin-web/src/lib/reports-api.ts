/**
 * Typed client for the Admin API's reports and exports surface (SRS 12.18,
 * tasks 128a-128d): `ReportsController`. Every call is authenticated - these
 * are admin-only endpoints gated behind "reports.read"/"reports.write"
 * server-side.
 */
import { API_V1, apiFetch, apiFetchBlob } from "./api";
import type {
  BookingRevenueReportResponse,
  CouponUsageReportResponse,
  CustomerSegmentationReportResponse,
  ExportJobStatusResponse,
  RefundReportResponse,
  RequestExportJobRequest,
  SupportTicketReportResponse,
} from "./reports-types";

const REPORTS_BASE = `${API_V1}/reports`;

function query(params: object): string {
  const entries = Object.entries(params as Record<string, string | number | boolean | undefined | null>)
    .filter(([, value]) => value !== undefined && value !== null && value !== "");
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString()}`;
}

/** Triggers a browser download of a CSV blob - shared by every "Export CSV" button on the reports page. */
export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export interface BookingRevenueFilters {
  fromUtc: string;
  toUtc: string;
  city?: string;
  categoryId?: string;
}

export const getBookingRevenueReport = (filters: BookingRevenueFilters) =>
  apiFetch<BookingRevenueReportResponse>(`${REPORTS_BASE}/booking-revenue${query(filters)}`, { authenticated: true });

export const exportBookingRevenueCsv = (filters: BookingRevenueFilters) =>
  apiFetchBlob(`${REPORTS_BASE}/booking-revenue/export${query(filters)}`, { authenticated: true });

export interface DateRangeFilters {
  fromUtc: string;
  toUtc: string;
}

export const getRefundReport = (filters: DateRangeFilters) =>
  apiFetch<RefundReportResponse>(`${REPORTS_BASE}/refunds${query(filters)}`, { authenticated: true });

export const exportRefundCsv = (filters: DateRangeFilters) =>
  apiFetchBlob(`${REPORTS_BASE}/refunds/export${query(filters)}`, { authenticated: true });

export const getCouponUsageReport = (filters: DateRangeFilters) =>
  apiFetch<CouponUsageReportResponse>(`${REPORTS_BASE}/coupon-usage${query(filters)}`, { authenticated: true });

export const exportCouponUsageCsv = (filters: DateRangeFilters) =>
  apiFetchBlob(`${REPORTS_BASE}/coupon-usage/export${query(filters)}`, { authenticated: true });

export interface CustomerSegmentationFilters {
  registeredFromUtc?: string;
  registeredToUtc?: string;
}

export const getCustomerSegmentationReport = (filters: CustomerSegmentationFilters) =>
  apiFetch<CustomerSegmentationReportResponse>(`${REPORTS_BASE}/customer-segmentation${query(filters)}`, {
    authenticated: true,
  });

export const exportCustomerSegmentationCsv = (filters: CustomerSegmentationFilters) =>
  apiFetchBlob(`${REPORTS_BASE}/customer-segmentation/export${query(filters)}`, { authenticated: true });

export const getSupportTicketReport = (filters: DateRangeFilters) =>
  apiFetch<SupportTicketReportResponse>(`${REPORTS_BASE}/support-tickets${query(filters)}`, { authenticated: true });

export const exportSupportTicketCsv = (filters: DateRangeFilters) =>
  apiFetchBlob(`${REPORTS_BASE}/support-tickets/export${query(filters)}`, { authenticated: true });

export const requestExportJob = (request: RequestExportJobRequest) =>
  apiFetch<ExportJobStatusResponse>(`${REPORTS_BASE}/exports`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const listMyExportJobs = () =>
  apiFetch<ExportJobStatusResponse[]>(`${REPORTS_BASE}/exports`, { authenticated: true });

export const getExportJobStatus = (jobId: string) =>
  apiFetch<ExportJobStatusResponse>(`${REPORTS_BASE}/exports/${jobId}`, { authenticated: true });

export const downloadExportJob = (jobId: string) =>
  apiFetchBlob(`${REPORTS_BASE}/exports/${jobId}/download`, { authenticated: true });
