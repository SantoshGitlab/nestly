import { ReviewStatus } from "./types";

/**
 * Filter/query-building helpers for the admin review moderation screen (SRS
 * 12.15, task 122). Mirrors lib/audit.ts's shape for the same kind of
 * filter-form-state-to-query-string job.
 */

export type ReviewStatusFilterValue = "" | "Visible" | "Hidden";

/** Order matches `Nestly.Domain.ReviewStatus`'s declaration order - sent as the readable member name; ASP.NET Core's query-string enum binder accepts it case-insensitively (same precedent as lib/audit.ts's AuditOutcome filter). */
export const REVIEW_STATUS_FILTER_OPTIONS: readonly { value: ReviewStatusFilterValue; label: string }[] = [
  { value: "", label: "Any status" },
  { value: "Visible", label: "Visible" },
  { value: "Hidden", label: "Hidden" },
];

export function reviewStatusLabel(status: ReviewStatus): string {
  return status === ReviewStatus.Hidden ? "Hidden" : "Visible";
}

export type FlaggedFilterValue = "" | "true" | "false";

export const FLAGGED_FILTER_OPTIONS: readonly { value: FlaggedFilterValue; label: string }[] = [
  { value: "", label: "Any" },
  { value: "true", label: "Flagged only" },
  { value: "false", label: "Not flagged" },
];

/**
 * Client-side filter form state (SRS 12.15's filter list: status, flagged,
 * rating range, date, service/category). `fromDate`/`toDate` hold plain
 * `yyyy-mm-dd` values straight out of an `<input type="date">`, converted to
 * full UTC instants only when building the query string - same convention as
 * `AuditLogFilters`.
 */
export interface ReviewModerationFilters {
  status: ReviewStatusFilterValue;
  flagged: FlaggedFilterValue;
  minRating: string;
  maxRating: string;
  fromDate: string;
  toDate: string;
  serviceId: string;
  categoryId: string;
}

export const DEFAULT_REVIEW_MODERATION_FILTERS: ReviewModerationFilters = {
  status: "",
  flagged: "",
  minRating: "",
  maxRating: "",
  fromDate: "",
  toDate: "",
  serviceId: "",
  categoryId: "",
};

/** Builds the query string for `GET {API_V1}/reviews` (and `/reviews/export`), omitting unset filters. */
export function buildReviewModerationQuery(
  filters: ReviewModerationFilters,
  paging: { page: number; pageSize: number },
): string {
  const params = new URLSearchParams();

  if (filters.status) params.set("status", filters.status);
  if (filters.flagged) params.set("isFlagged", filters.flagged);
  if (filters.minRating) params.set("minRating", filters.minRating);
  if (filters.maxRating) params.set("maxRating", filters.maxRating);
  if (filters.serviceId.trim()) params.set("serviceId", filters.serviceId.trim());
  if (filters.categoryId.trim()) params.set("categoryId", filters.categoryId.trim());

  // <input type="date"> yields "yyyy-mm-dd"; the API filters on a full UTC
  // instant, so pin From to the start of that day and To to its end -
  // otherwise "toUtc=2026-07-31" would exclude every review posted that day.
  if (filters.fromDate) params.set("fromUtc", `${filters.fromDate}T00:00:00.000Z`);
  if (filters.toDate) params.set("toUtc", `${filters.toDate}T23:59:59.999Z`);

  params.set("page", String(paging.page));
  params.set("pageSize", String(paging.pageSize));

  return params.toString();
}
