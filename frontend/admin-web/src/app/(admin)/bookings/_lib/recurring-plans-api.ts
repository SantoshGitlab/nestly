import { API_V1, apiFetch } from "@/lib/api";

/**
 * Typed client for the admin recurring-plan surface (task 299):
 * `GET /admin/recurring-plans` and `GET /admin/recurring-plans/report`.
 *
 * Lives under `bookings/_lib` rather than `src/lib` for the same reason
 * `nestly-coins/_lib/coins-api.ts` does - nothing outside this module consumes
 * it, and the shared clients in `src/lib` each back a whole SRS section.
 * Recurring plans are not a section of their own: they are a way bookings come
 * into existence, which is also why they sit behind `bookings.read` rather
 * than a permission of their own (see RecurringPlansController's doc comment).
 */

/**
 * Mirrors Nestly.Domain.RecurringBookingPlanStatus's declaration order.
 * admin-api registers no JsonStringEnumConverter, so this enum crosses the
 * wire as its ordinal - same convention as `lib/types.ts`'s BookingStatus.
 * Keep in sync with the C# enum if its order ever changes.
 */
export enum RecurringPlanStatus {
  Active = 0,
  Paused = 1,
  Cancelled = 2,
  Completed = 3,
}

/** Mirrors Nestly.Domain.RecurringBookingRecurrenceFrequency's declaration order. */
export enum RecurrenceFrequency {
  Weekly = 0,
  Biweekly = 1,
  Monthly = 2,
}

export const PLAN_STATUS_LABELS: Record<RecurringPlanStatus, string> = {
  [RecurringPlanStatus.Active]: "Active",
  [RecurringPlanStatus.Paused]: "Paused",
  [RecurringPlanStatus.Cancelled]: "Cancelled",
  [RecurringPlanStatus.Completed]: "Completed",
};

export const FREQUENCY_LABELS: Record<RecurrenceFrequency, string> = {
  [RecurrenceFrequency.Weekly]: "Weekly",
  [RecurrenceFrequency.Biweekly]: "Every 2 weeks",
  [RecurrenceFrequency.Monthly]: "Monthly",
};

const DAY_NAMES = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
] as const;

/**
 * "Weekly on Tuesday" / "Monthly on the 11th" — the cadence phrased the way a
 * human reads a standing appointment, rather than three columns the reader has
 * to recombine themselves. `DayOfWeek` crosses the wire as its .NET ordinal,
 * which is Sunday-first.
 */
export function describeCadence(plan: {
  frequency: RecurrenceFrequency;
  recurrenceDayOfWeek: number | null;
  recurrenceDayOfMonth: number | null;
}): string {
  const base = FREQUENCY_LABELS[plan.frequency] ?? String(plan.frequency);
  if (plan.recurrenceDayOfWeek !== null) {
    const day = DAY_NAMES[plan.recurrenceDayOfWeek];
    return day ? `${base} on ${day}` : base;
  }
  if (plan.recurrenceDayOfMonth !== null) {
    return `${base} on day ${plan.recurrenceDayOfMonth}`;
  }
  return base;
}

export interface RecurringPlanListItem {
  id: string;
  customerId: string;
  customerName: string;
  serviceId: string;
  serviceName: string;
  frequency: RecurrenceFrequency;
  recurrenceDayOfWeek: number | null;
  recurrenceDayOfMonth: number | null;
  startDate: string;
  endDate: string | null;
  occurrenceCount: number | null;
  completedOccurrenceCount: number;
  nextOccurrenceDate: string;
  status: RecurringPlanStatus;
  createdAtUtc: string;
}

export interface RecurringPlanSearchResponse {
  items: RecurringPlanListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface RecurringPlanStatusCount {
  status: RecurringPlanStatus;
  planCount: number;
}

export interface RecurringPlanFrequencyCount {
  frequency: RecurrenceFrequency;
  planCount: number;
}

export interface RecurringPlanDailyVolume {
  slotDate: string;
  bookingCount: number;
}

export interface RecurringPlanReport {
  totalPlans: number;
  byStatus: RecurringPlanStatusCount[];
  activeByFrequency: RecurringPlanFrequencyCount[];
  horizonFromDate: string;
  horizonToDate: string;
  plansDueInHorizon: number;
  upcomingOccurrenceVolume: number;
  upcomingVolumeByDate: RecurringPlanDailyVolume[];
}

export interface RecurringPlanSearchParams {
  status?: string;
  frequency?: string;
  page: number;
  pageSize: number;
}

const BASE = `${API_V1}/recurring-plans`;

export function searchRecurringPlans(
  params: RecurringPlanSearchParams,
): Promise<RecurringPlanSearchResponse> {
  const query = new URLSearchParams({
    page: String(params.page),
    pageSize: String(params.pageSize),
  });
  if (params.status) query.set("status", params.status);
  if (params.frequency) query.set("frequency", params.frequency);

  return apiFetch<RecurringPlanSearchResponse>(`${BASE}?${query.toString()}`, {
    authenticated: true,
  });
}

/**
 * The horizon is sent as plain `yyyy-mm-dd`, not as a UTC instant: the backend
 * takes `DateOnly` and compares against a booking's slot *date*, which is a
 * calendar day rather than a moment (unlike the coins/coupon reports, whose
 * ranges are `DateTime` and so go through `lib/day-range`). Omitting both ends
 * asks the server for its own default horizon.
 */
export function getRecurringPlanReport(
  fromDate?: string,
  toDate?: string,
): Promise<RecurringPlanReport> {
  const query = new URLSearchParams();
  if (fromDate) query.set("fromDate", fromDate);
  if (toDate) query.set("toDate", toDate);
  const suffix = query.toString() ? `?${query.toString()}` : "";

  return apiFetch<RecurringPlanReport>(`${BASE}/report${suffix}`, { authenticated: true });
}
