/**
 * Typed client for the Admin API's booking-management surface (SRS 12.11,
 * 12.13.2-3; tasks 115a-117c): `BookingsController` - filterable search,
 * full detail/timeline, general status updates, and the cancel/reschedule/
 * refund actions. Every call is authenticated - these are admin-only
 * endpoints gated behind the "bookings" permission module server-side.
 */
import { API_V1, apiFetch } from "./api";
import type {
  AdminBookingDetail,
  AdminBookingSearchParams,
  AdminBookingSearchResponse,
  AdminBookingStatusUpdateRequest,
  AdminBookingTrackingResponse,
  AdminCancelBookingRequest,
  AdminRefundRequest,
  AdminRescheduleBookingRequest,
  BookingCompletionProofResponse,
} from "./bookings-types";

const BOOKINGS_BASE = `${API_V1}/bookings`;

// Parameter typed as `object` (not `Record<string, ...>`) so that named
// interfaces like AdminBookingSearchParams - which have no index signature
// of their own - can be passed in without a cast; matches coupon-api.ts's
// query() helper.
function query(params: object): string {
  const entries = Object.entries(params as Record<string, string | number | boolean | undefined>)
    .filter(([, value]) => value !== undefined);
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString()}`;
}

export const searchBookings = (params: AdminBookingSearchParams) =>
  apiFetch<AdminBookingSearchResponse>(`${BOOKINGS_BASE}${query(params)}`, { authenticated: true });

export const getBookingDetail = (bookingId: string) =>
  apiFetch<AdminBookingDetail>(`${BOOKINGS_BASE}/${bookingId}`, { authenticated: true });

export const updateBookingStatus = (bookingId: string, request: AdminBookingStatusUpdateRequest) =>
  apiFetch<AdminBookingDetail>(`${BOOKINGS_BASE}/${bookingId}/status`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const cancelBooking = (bookingId: string, request: AdminCancelBookingRequest) =>
  apiFetch<AdminBookingDetail>(`${BOOKINGS_BASE}/${bookingId}/cancel`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const rescheduleBooking = (bookingId: string, request: AdminRescheduleBookingRequest) =>
  apiFetch<AdminBookingDetail>(`${BOOKINGS_BASE}/${bookingId}/reschedule`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const refundBooking = (bookingId: string, request: AdminRefundRequest) =>
  apiFetch<AdminBookingDetail>(`${BOOKINGS_BASE}/${bookingId}/refund`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

/**
 * Completion proof (photos + checklist) for a booking, if any (tasks 195-198
 * dispute review). Normalised to `null` rather than letting apiFetch's
 * `undefined` leak out: the endpoint answers 204 until the provider has
 * submitted proof, and React Query rejects an `undefined` resolution
 * ("Query data cannot be undefined"), which put the proof card into an error
 * state on every booking that simply has no proof yet.
 */
export const getBookingCompletionProof = async (
  bookingId: string,
): Promise<BookingCompletionProofResponse | null> => {
  const result = await apiFetch<BookingCompletionProofResponse | undefined>(
    `${BOOKINGS_BASE}/${bookingId}/completion-proof`,
    { authenticated: true },
  );
  return result ?? null;
};

/** Live tracking snapshot for the ops view (task 284). Rejects with a 404 ApiError - see AdminBookingTrackingResponse's doc comment - when there is no live data to show; the caller renders that as a plain state, not an error. */
export const getBookingTracking = (bookingId: string) =>
  apiFetch<AdminBookingTrackingResponse>(`${BOOKINGS_BASE}/${bookingId}/tracking`, { authenticated: true });
