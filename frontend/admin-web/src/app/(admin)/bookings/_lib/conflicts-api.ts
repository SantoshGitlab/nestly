import { API_V1, apiFetch } from "@/lib/api";
import { BookingStatus } from "@/lib/types";

/**
 * Typed client for the admin booking-conflict surface (task 321/322):
 * `GET /admin/booking-conflicts` and `GET /admin/booking-conflicts/count`.
 *
 * Lives under `bookings/_lib` for the same reason `recurring-plans-api.ts`
 * does - nothing outside this module consumes it, and a double-booking is not
 * a domain of its own but a fault in how bookings were assigned, which is why
 * it also sits behind `bookings.read` rather than a permission of its own.
 *
 * Read-only on purpose. Resolving a conflict is an ordinary reassignment, so
 * this screen writes through `providers-api`'s existing
 * `assignProviderToBooking` rather than a "resolve" endpoint of its own - see
 * BookingConflictsController's doc comment.
 */

/**
 * Mirrors Nestly.Domain.BookingProviderAssignmentStatus's declaration order.
 * admin-api registers no JsonStringEnumConverter, so this enum crosses the
 * wire as its ordinal - same convention as `lib/types.ts`'s BookingStatus.
 * Keep in sync with the C# enum if its order ever changes.
 */
export enum AssignmentStatus {
  Assigned = 0,
  Accepted = 1,
  Rejected = 2,
  Reassigned = 3,
  Withdrawn = 4,
}

/** Mirrors Nestly.Domain.BookingAssignedByType's declaration order. */
export enum AssignedByType {
  Admin = 0,
  System = 1,
}

export const ASSIGNMENT_STATUS_LABELS: Record<AssignmentStatus, string> = {
  [AssignmentStatus.Assigned]: "Offered",
  [AssignmentStatus.Accepted]: "Accepted",
  [AssignmentStatus.Rejected]: "Rejected",
  [AssignmentStatus.Reassigned]: "Superseded",
  [AssignmentStatus.Withdrawn]: "Withdrawn",
};

export const ASSIGNED_BY_LABELS: Record<AssignedByType, string> = {
  [AssignedByType.Admin]: "Admin",
  [AssignedByType.System]: "Auto-assigned",
};

export interface ConflictedBooking {
  bookingId: string;
  assignmentId: string;
  assignmentStatus: AssignmentStatus;
  bookingStatus: BookingStatus;
  assignedByType: AssignedByType;
  assignedAt: string;
  customerName: string;
  serviceName: string;
  slotDate: string;
  /** `HH:mm:ss` - a TimeSpan, not an instant. */
  startTime: string;
  endTime: string;
}

export interface BookingConflictGroup {
  providerId: string;
  providerDisplayName: string;
  providerPhone: string;
  slotDate: string;
  windowStart: string;
  windowEnd: string;
  bookings: ConflictedBooking[];
}

export interface BookingConflictSearchResponse {
  items: BookingConflictGroup[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface BookingConflictCountResponse {
  conflictCount: number;
}

export interface BookingConflictSearchParams {
  fromDate?: string;
  toDate?: string;
  page: number;
  pageSize: number;
}

const BASE = `${API_V1}/booking-conflicts`;

/**
 * Dates are sent as plain `yyyy-mm-dd`: the backend takes `DateOnly` and
 * compares against a booking's slot *date*, a calendar day rather than a
 * moment - same reasoning as the recurring-plan report, and the reason neither
 * goes through `lib/day-range`.
 */
export function searchBookingConflicts(
  params: BookingConflictSearchParams,
): Promise<BookingConflictSearchResponse> {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  });
  if (params.fromDate) query.set("fromDate", params.fromDate);
  if (params.toDate) query.set("toDate", params.toDate);

  return apiFetch<BookingConflictSearchResponse>(`${BASE}?${query.toString()}`, {
    authenticated: true,
  });
}

export function getBookingConflictCount(fromDate?: string): Promise<BookingConflictCountResponse> {
  const query = fromDate ? `?fromDate=${encodeURIComponent(fromDate)}` : "";
  return apiFetch<BookingConflictCountResponse>(`${BASE}/count${query}`, { authenticated: true });
}

/** `09:00:00` -> `09:00`; the seconds are always zero and only add noise in a table. */
export function formatSlotTime(time: string): string {
  return time.length >= 5 ? time.slice(0, 5) : time;
}
