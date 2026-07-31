/**
 * Typed client for the Admin API's ticket workflow surface (SRS 12.14, 16.2,
 * tasks 120a-f): `SupportTicketsController` - search/detail, assign/unassign,
 * respond, escalate, resolve/close, and link a booking. Every call is
 * authenticated - these are admin-only endpoints gated behind the "support"
 * permission module server-side. The formal dispute mark/resolve sub-flow
 * (task 155, `SupportTicketDisputesController`) is a separate, un-prefixed
 * route (`/api/v1/support-tickets/{id}/dispute`) not covered here.
 */
import { API_V1, apiFetch } from "./api";
import type {
  AddSupportTicketCommentRequestBody,
  AdminSupportTicketDetailResponse,
  AdminSupportTicketSearchResponse,
  AssignableAdminResponse,
  AssignSupportTicketRequestBody,
  LinkSupportTicketBookingRequestBody,
  ResolveSupportTicketRequestBody,
} from "./support-types";

const SUPPORT_TICKETS_BASE = `${API_V1}/support-tickets`;

/**
 * Takes an already-built query string (see lib/support.ts's
 * `buildSupportTicketSearchQuery`, same split as lib/reviews.ts's
 * `buildReviewModerationQuery`) rather than a typed params object - the
 * filter form's draft state is all strings (straight out of <select>/<input>
 * elements) and status/category/priority are sent as their readable enum
 * names (ASP.NET Core's query-string enum binder accepts these
 * case-insensitively, same precedent as lib/audit.ts), which doesn't map
 * cleanly onto the numeric-ordinal-typed response enums this file also
 * exports.
 */
export const searchSupportTickets = (queryString: string) =>
  apiFetch<AdminSupportTicketSearchResponse>(`${SUPPORT_TICKETS_BASE}?${queryString}`, { authenticated: true });

export const getSupportTicket = (id: string) =>
  apiFetch<AdminSupportTicketDetailResponse>(`${SUPPORT_TICKETS_BASE}/${id}`, { authenticated: true });

export const listAssignableAdmins = () =>
  apiFetch<AssignableAdminResponse[]>(`${SUPPORT_TICKETS_BASE}/assignable-admins`, { authenticated: true });

export const assignSupportTicket = (id: string, body: AssignSupportTicketRequestBody) =>
  apiFetch<AdminSupportTicketDetailResponse>(`${SUPPORT_TICKETS_BASE}/${id}/assign`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(body),
  });

export const unassignSupportTicket = (id: string) =>
  apiFetch<AdminSupportTicketDetailResponse>(`${SUPPORT_TICKETS_BASE}/${id}/unassign`, {
    method: "POST",
    authenticated: true,
  });

export const respondToSupportTicket = (id: string, body: AddSupportTicketCommentRequestBody) =>
  apiFetch<AdminSupportTicketDetailResponse>(`${SUPPORT_TICKETS_BASE}/${id}/respond`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(body),
  });

export const escalateSupportTicket = (id: string) =>
  apiFetch<AdminSupportTicketDetailResponse>(`${SUPPORT_TICKETS_BASE}/${id}/escalate`, {
    method: "POST",
    authenticated: true,
  });

export const resolveSupportTicket = (id: string, body: ResolveSupportTicketRequestBody) =>
  apiFetch<AdminSupportTicketDetailResponse>(`${SUPPORT_TICKETS_BASE}/${id}/resolve`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(body),
  });

export const closeSupportTicket = (id: string) =>
  apiFetch<AdminSupportTicketDetailResponse>(`${SUPPORT_TICKETS_BASE}/${id}/close`, {
    method: "POST",
    authenticated: true,
  });

export const linkSupportTicketBooking = (id: string, body: LinkSupportTicketBookingRequestBody) =>
  apiFetch<AdminSupportTicketDetailResponse>(`${SUPPORT_TICKETS_BASE}/${id}/link-booking`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(body),
  });
