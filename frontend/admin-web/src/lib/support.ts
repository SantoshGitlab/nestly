import { SupportTicketCategory, SupportTicketPriority, SupportTicketStatus } from "./types";

/**
 * Filter/query-building and label helpers for the admin ticket workflow
 * screens (SRS 12.14, 16.2, tasks 120f, 121). Mirrors lib/reviews.ts's shape
 * for the same kind of filter-form-state-to-query-string job.
 */

export type StatusFilterValue = "" | keyof typeof SupportTicketStatus;
export type CategoryFilterValue = "" | keyof typeof SupportTicketCategory;
export type PriorityFilterValue = "" | keyof typeof SupportTicketPriority;
export type UnassignedFilterValue = "" | "true" | "false";

export const STATUS_FILTER_OPTIONS: readonly { value: StatusFilterValue; label: string }[] = [
  { value: "", label: "Any status" },
  { value: "Open", label: "Open" },
  { value: "InProgress", label: "In Progress" },
  { value: "WaitingForCustomer", label: "Waiting for Customer" },
  { value: "Escalated", label: "Escalated" },
  { value: "Resolved", label: "Resolved" },
  { value: "Closed", label: "Closed" },
];

export const CATEGORY_FILTER_OPTIONS: readonly { value: CategoryFilterValue; label: string }[] = [
  { value: "", label: "Any category" },
  { value: "BookingIssue", label: "Booking Issue" },
  { value: "PaymentIssue", label: "Payment Issue" },
  { value: "RefundIssue", label: "Refund Issue" },
  { value: "ServiceQuality", label: "Service Quality" },
  { value: "ProfessionalConduct", label: "Professional Conduct" },
  { value: "PricingDispute", label: "Pricing Dispute" },
  { value: "TechnicalIssue", label: "Technical Issue" },
  { value: "GeneralInquiry", label: "General Inquiry" },
];

export const PRIORITY_FILTER_OPTIONS: readonly { value: PriorityFilterValue; label: string }[] = [
  { value: "", label: "Any priority" },
  { value: "Low", label: "Low" },
  { value: "Normal", label: "Normal" },
  { value: "High", label: "High" },
  { value: "Urgent", label: "Urgent" },
];

export const UNASSIGNED_FILTER_OPTIONS: readonly { value: UnassignedFilterValue; label: string }[] = [
  { value: "", label: "Any" },
  { value: "true", label: "Unassigned only" },
  { value: "false", label: "Assigned only" },
];

export function statusLabel(status: SupportTicketStatus): string {
  return STATUS_FILTER_OPTIONS.find((o) => o.value === SupportTicketStatus[status])?.label ?? "Unknown";
}

export function categoryLabel(category: SupportTicketCategory): string {
  return CATEGORY_FILTER_OPTIONS.find((o) => o.value === SupportTicketCategory[category])?.label ?? "Unknown";
}

export function priorityLabel(priority: SupportTicketPriority): string {
  return PRIORITY_FILTER_OPTIONS.find((o) => o.value === SupportTicketPriority[priority])?.label ?? "Unknown";
}

/** Client-side filter form state for the admin ticket list (SRS 12.14.1). `fromDate`/`toDate` hold plain `yyyy-mm-dd` values straight out of an `<input type="date">`, converted to full UTC instants only when building the query string - same convention as `ReviewModerationFilters`. */
export interface SupportTicketFilters {
  status: StatusFilterValue;
  category: CategoryFilterValue;
  priority: PriorityFilterValue;
  customerId: string;
  bookingId: string;
  assignedAdminUserId: string;
  unassigned: UnassignedFilterValue;
  fromDate: string;
  toDate: string;
}

export const DEFAULT_SUPPORT_TICKET_FILTERS: SupportTicketFilters = {
  status: "",
  category: "",
  priority: "",
  customerId: "",
  bookingId: "",
  assignedAdminUserId: "",
  unassigned: "",
  fromDate: "",
  toDate: "",
};

/** Builds the query string for `GET {API_V1}/support-tickets`, omitting unset filters. */
export function buildSupportTicketSearchQuery(
  filters: SupportTicketFilters,
  paging: { page: number; pageSize: number },
): string {
  const params = new URLSearchParams();

  if (filters.status) params.set("status", filters.status);
  if (filters.category) params.set("category", filters.category);
  if (filters.priority) params.set("priority", filters.priority);
  if (filters.customerId.trim()) params.set("customerId", filters.customerId.trim());
  if (filters.bookingId.trim()) params.set("bookingId", filters.bookingId.trim());
  if (filters.assignedAdminUserId.trim()) params.set("assignedAdminUserId", filters.assignedAdminUserId.trim());
  if (filters.unassigned) params.set("unassigned", filters.unassigned);

  // <input type="date"> yields "yyyy-mm-dd"; the API filters on a full UTC
  // instant, so pin From to the start of that day and To to its end - same
  // convention as buildReviewModerationQuery.
  if (filters.fromDate) params.set("fromUtc", `${filters.fromDate}T00:00:00.000Z`);
  if (filters.toDate) params.set("toUtc", `${filters.toDate}T23:59:59.999Z`);

  params.set("page", String(paging.page));
  params.set("pageSize", String(paging.pageSize));

  return params.toString();
}
