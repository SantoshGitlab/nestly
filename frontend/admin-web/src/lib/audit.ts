/**
 * Types and helpers for the audit-log-viewer (task 130, SRS 21).
 *
 * Mirrors backend/shared/Application/Auditing/AuditLogContracts.cs
 * (`AuditLogFilterRequest`, `AuditLogEntryResponse`, `PagedAuditLogResponse`).
 * Field names are camelCase to match ASP.NET Core's default System.Text.Json
 * naming policy (see lib/types.ts's AdminLoginResponse for the precedent).
 *
 * `actorType` and `outcome` come back from the API as their C# enum's
 * underlying numeric value - no `JsonStringEnumConverter` is registered
 * anywhere in this codebase (`SupportTicketSummaryResponse`'s enums
 * serialize the same bare-number way), so this file matches existing
 * behaviour rather than inventing a one-off string-enum convention just for
 * this screen. The *request* side is unaffected: ASP.NET Core's query-string
 * enum binder accepts the member name case-insensitively, so filters are
 * still sent as the readable names below ("AdminUser", "Deny", ...).
 */

export type AuditActorTypeName = "Anonymous" | "Customer" | "AdminUser" | "System";

/** Order matches `Nestly.Domain.AuditActorType`'s declaration order (its numeric value). */
export const AUDIT_ACTOR_TYPES: readonly AuditActorTypeName[] = [
  "Anonymous",
  "Customer",
  "AdminUser",
  "System",
];

export type AuditOutcomeName = "Grant" | "Deny" | "Success" | "Failure";

/** Order matches `Nestly.Application.Auditing.AuditOutcome`'s declaration order (its numeric value). */
export const AUDIT_OUTCOMES: readonly AuditOutcomeName[] = ["Grant", "Deny", "Success", "Failure"];

export function actorTypeLabel(value: number): AuditActorTypeName {
  return AUDIT_ACTOR_TYPES[value] ?? "Anonymous";
}

export function outcomeLabel(value: number): AuditOutcomeName {
  return AUDIT_OUTCOMES[value] ?? "Success";
}

/** One audit trail row (SRS 21.2's minimum fields). */
export interface AuditLogEntry {
  id: string;
  actorType: number;
  actorId: string | null;
  entityName: string;
  entityId: string;
  action: string;
  outcome: number;
  oldValues: string | null;
  newValues: string | null;
  ipAddress: string | null;
  correlationId: string | null;
  occurredOnUtc: string;
}

export interface PagedAuditLogResponse {
  items: AuditLogEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/**
 * Client-side filter state. `fromDate`/`toDate` hold plain `yyyy-mm-dd`
 * values straight out of an `<input type="date">` - converted to full UTC
 * instants only when building the query string (see `buildAuditLogQuery`).
 */
export interface AuditLogFilters {
  actorType: AuditActorTypeName | "";
  actorId: string;
  entityName: string;
  action: string;
  outcome: AuditOutcomeName | "";
  fromDate: string;
  toDate: string;
  page: number;
  pageSize: number;
}

export const DEFAULT_AUDIT_LOG_FILTERS: AuditLogFilters = {
  actorType: "",
  actorId: "",
  entityName: "",
  action: "",
  outcome: "",
  fromDate: "",
  toDate: "",
  page: 1,
  pageSize: 25,
};

/** Builds the query string for `GET {API_V1}/audit-log`, omitting unset filters. */
export function buildAuditLogQuery(filters: AuditLogFilters): string {
  const params = new URLSearchParams();

  if (filters.actorType) params.set("actorType", filters.actorType);
  if (filters.actorId.trim()) params.set("actorId", filters.actorId.trim());
  if (filters.entityName.trim()) params.set("entityName", filters.entityName.trim());
  if (filters.action.trim()) params.set("action", filters.action.trim());
  if (filters.outcome) params.set("outcome", filters.outcome);

  // <input type="date"> yields "yyyy-mm-dd"; the API filters on a full UTC
  // instant, so pin From to the start of that day and To to its end -
  // otherwise "toDate=2026-07-31" would exclude every event that actually
  // happened during that day.
  if (filters.fromDate) params.set("fromUtc", `${filters.fromDate}T00:00:00.000Z`);
  if (filters.toDate) params.set("toUtc", `${filters.toDate}T23:59:59.999Z`);

  params.set("page", String(filters.page));
  params.set("pageSize", String(filters.pageSize));

  return params.toString();
}
