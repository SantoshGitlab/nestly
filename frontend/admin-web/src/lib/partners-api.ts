/**
 * Typed client for the Admin API's partner-management surface (PARTNER.md;
 * tasks 147, 148, 150a-c, 160): `PartnersController` - CRUD, KYC approval,
 * background check/activation, performance and earnings - plus
 * `PayoutsController`. Every call is authenticated - these are admin-only
 * endpoints gated behind the "partner"/"payout" permission modules
 * server-side.
 */
import { API_V1, apiFetch } from "./api";
import type {
  AssignPartnerRequest,
  BookingPartnerAssignment,
  CreatePartnerPayoutRequest,
  CreatePartnerRequest,
  PartnerDetail,
  PartnerEarningsSummary,
  PartnerKycDocument,
  PartnerPayout,
  PartnerPayoutSearchResponse,
  PartnerPerformance,
  PartnerSearchParams,
  PartnerSearchResponse,
  PartnerBackgroundCheck,
  PartnerPayoutStatus,
  RecordBackgroundCheckRequest,
  RecordPartnerEarningAdjustmentRequest,
  RejectAssignmentRequest,
  RejectPartnerKycDocumentRequest,
  SuspendPartnerRequest,
  UpdatePartnerPayoutStatusRequest,
  UpdatePartnerRequest,
} from "./partners-types";

const PARTNERS_BASE = `${API_V1}/partners`;
const PAYOUTS_BASE = `${API_V1}/payouts`;
const BOOKINGS_BASE = `${API_V1}/bookings`;

// Mirrors bookings-api.ts's query() helper.
function query(params: object): string {
  const entries = Object.entries(params as Record<string, string | number | boolean | undefined>)
    .filter(([, value]) => value !== undefined);
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString()}`;
}

// ---- CRUD (task 150a) ----

export const searchPartners = (params: PartnerSearchParams) =>
  apiFetch<PartnerSearchResponse>(`${PARTNERS_BASE}${query(params)}`, { authenticated: true });

export const getPartnerDetail = (partnerId: string) =>
  apiFetch<PartnerDetail>(`${PARTNERS_BASE}/${partnerId}`, { authenticated: true });

export const createPartner = (request: CreatePartnerRequest) =>
  apiFetch<PartnerDetail>(PARTNERS_BASE, { method: "POST", authenticated: true, body: JSON.stringify(request) });

export const updatePartner = (partnerId: string, request: UpdatePartnerRequest) =>
  apiFetch<PartnerDetail>(`${PARTNERS_BASE}/${partnerId}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const suspendPartner = (partnerId: string, request: SuspendPartnerRequest) =>
  apiFetch<PartnerDetail>(`${PARTNERS_BASE}/${partnerId}/suspend`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const reactivatePartner = (partnerId: string) =>
  apiFetch<PartnerDetail>(`${PARTNERS_BASE}/${partnerId}/reactivate`, { method: "POST", authenticated: true });

// ---- KYC approval, background check, activation (task 150b, 160) ----

export const approveKycDocument = (documentId: string) =>
  apiFetch<PartnerKycDocument>(`${PARTNERS_BASE}/kyc-documents/${documentId}/approve`, {
    method: "POST",
    authenticated: true,
  });

export const rejectKycDocument = (documentId: string, request: RejectPartnerKycDocumentRequest) =>
  apiFetch<PartnerKycDocument>(`${PARTNERS_BASE}/kyc-documents/${documentId}/reject`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const recordBackgroundCheck = (partnerId: string, request: RecordBackgroundCheckRequest) =>
  apiFetch<PartnerBackgroundCheck>(`${PARTNERS_BASE}/${partnerId}/background-check`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const activatePartner = (partnerId: string) =>
  apiFetch<PartnerDetail>(`${PARTNERS_BASE}/${partnerId}/activate`, { method: "POST", authenticated: true });

// ---- Performance and earnings (task 150c, 148) ----

export const getPartnerPerformance = (partnerId: string) =>
  apiFetch<PartnerPerformance>(`${PARTNERS_BASE}/${partnerId}/performance`, { authenticated: true });

export const getPartnerEarnings = (partnerId: string) =>
  apiFetch<PartnerEarningsSummary>(`${PARTNERS_BASE}/${partnerId}/earnings`, { authenticated: true });

export const recordEarningAdjustment = (partnerId: string, request: RecordPartnerEarningAdjustmentRequest) =>
  apiFetch<PartnerEarningsSummary>(`${PARTNERS_BASE}/${partnerId}/earnings`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

// ---- Payouts (task 148) ----

export const searchPayouts = (partnerId: string, status?: PartnerPayoutStatus) =>
  apiFetch<PartnerPayoutSearchResponse>(`${PAYOUTS_BASE}${query({ partnerId, status })}`, { authenticated: true });

export const createPayoutBatch = (partnerId: string, request: CreatePartnerPayoutRequest) =>
  apiFetch<PartnerPayout>(`${PAYOUTS_BASE}/partners/${partnerId}`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updatePayoutStatus = (payoutId: string, request: UpdatePartnerPayoutStatusRequest) =>
  apiFetch<PartnerPayout>(`${PAYOUTS_BASE}/${payoutId}/status`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

// ---- Booking assignment (task 147, 159 - used from the booking detail screen) ----

export const assignPartnerToBooking = (bookingId: string, request: AssignPartnerRequest) =>
  apiFetch<BookingPartnerAssignment>(`${BOOKINGS_BASE}/${bookingId}/assign-partner`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const rejectBookingAssignment = (bookingId: string, request: RejectAssignmentRequest) =>
  apiFetch<BookingPartnerAssignment>(`${BOOKINGS_BASE}/${bookingId}/reject-assignment`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const getBookingAssignmentHistory = (bookingId: string) =>
  apiFetch<BookingPartnerAssignment[]>(`${BOOKINGS_BASE}/${bookingId}/assignments`, { authenticated: true });
