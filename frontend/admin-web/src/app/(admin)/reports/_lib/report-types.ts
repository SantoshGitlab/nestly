import { ExportReportType } from "@/lib/reports-types";

/** Report labels, shared by the async-export request form and its job list. */
const REPORT_TYPE_LABELS: Record<ExportReportType, string> = {
  [ExportReportType.BookingRevenue]: "Booking & Revenue",
  [ExportReportType.RefundUsage]: "Refunds",
  [ExportReportType.CouponUsage]: "Coupon usage",
  [ExportReportType.CustomerSegmentation]: "Customer segmentation",
  [ExportReportType.SupportTicket]: "Support tickets",
};

/** Declaration order, so the Select reads in the same order as the cards above it. */
const REPORT_TYPES: readonly ExportReportType[] = [
  ExportReportType.BookingRevenue,
  ExportReportType.RefundUsage,
  ExportReportType.CouponUsage,
  ExportReportType.CustomerSegmentation,
  ExportReportType.SupportTicket,
];

export const REPORT_TYPE_OPTIONS = REPORT_TYPES.map((type) => ({
  value: String(type),
  label: REPORT_TYPE_LABELS[type],
}));

export function reportTypeLabel(type: ExportReportType): string {
  return REPORT_TYPE_LABELS[type] ?? "Report";
}
