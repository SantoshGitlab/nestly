/**
 * Response/request shapes for the Admin API's slot configuration surface
 * (SRS 12.10, tasks 113a-e): `SlotsController`. Mirrors the backend contracts
 * in `Application/Slots/SlotManagementContracts.cs` field for field.
 *
 * AdminApi has no JsonStringEnumConverter registered (see lib/types.ts's
 * BookingStatus doc comment), so every enum below serialises over the wire
 * as its ordinal and must stay in declaration-order sync with its C# source.
 */

/** Mirrors the .NET BCL System.DayOfWeek enum's declaration order exactly (Sunday = 0). */
export enum DayOfWeek {
  Sunday = 0,
  Monday = 1,
  Tuesday = 2,
  Wednesday = 3,
  Thursday = 4,
  Friday = 5,
  Saturday = 6,
}

export const DAY_OF_WEEK_LABELS: Record<DayOfWeek, string> = {
  [DayOfWeek.Sunday]: "Sun",
  [DayOfWeek.Monday]: "Mon",
  [DayOfWeek.Tuesday]: "Tue",
  [DayOfWeek.Wednesday]: "Wed",
  [DayOfWeek.Thursday]: "Thu",
  [DayOfWeek.Friday]: "Fri",
  [DayOfWeek.Saturday]: "Sat",
};

export const ALL_DAYS_OF_WEEK: readonly DayOfWeek[] = [
  DayOfWeek.Sunday,
  DayOfWeek.Monday,
  DayOfWeek.Tuesday,
  DayOfWeek.Wednesday,
  DayOfWeek.Thursday,
  DayOfWeek.Friday,
  DayOfWeek.Saturday,
];

/** Mirrors Nestly.Domain.SlotBlackoutType's declaration order exactly. */
export enum SlotBlackoutType {
  Holiday = 0,
  Blackout = 1,
}

export const SLOT_BLACKOUT_TYPE_LABELS: Record<SlotBlackoutType, string> = {
  [SlotBlackoutType.Holiday]: "Holiday",
  [SlotBlackoutType.Blackout]: "Blackout",
};

// ---- Lookups ----

export interface SlotCityLookupResponse {
  id: string;
  name: string;
}

export interface CategoryLookupResponse {
  id: string;
  name: string;
}

export interface ServiceLookupResponse {
  id: string;
  name: string;
}

// ---- Windows (task 113a) ----

export interface SlotWindowAdminResponse {
  id: string;
  cityId: string;
  cityName: string;
  name: string;
  startTime: string;
  endTime: string;
  isActive: boolean;
  maxBookingsPerSlot: number | null;
  daysOfWeek: DayOfWeek[];
}

export interface SlotWindowCreateRequest {
  cityId: string;
  name: string;
  startTime: string;
  endTime: string;
  maxBookingsPerSlot: number | null;
  daysOfWeek: DayOfWeek[];
}

export interface SlotWindowUpdateRequest {
  name: string;
  startTime: string;
  endTime: string;
  daysOfWeek: DayOfWeek[];
}

// ---- Capacity (task 113d) ----

export interface SlotWindowCapacityUpdateRequest {
  maxBookingsPerSlot: number | null;
}

// ---- Blackouts (task 113b) ----

export interface SlotBlackoutAdminResponse {
  id: string;
  cityId: string;
  cityName: string;
  startDate: string;
  endDate: string;
  type: SlotBlackoutType;
  reason: string | null;
}

export interface SlotBlackoutCreateRequest {
  cityId: string;
  startDate: string;
  endDate: string;
  type: SlotBlackoutType;
  reason: string | null;
}

// ---- Cutoffs / advance-booking policy (task 113c) ----

export interface SlotBookingPolicyAdminResponse {
  id: string;
  cityId: string;
  cityName: string;
  cutoffMinutes: number;
  maxAdvanceDays: number;
}

export interface SlotBookingPolicyUpsertRequest {
  cityId: string;
  cutoffMinutes: number;
  maxAdvanceDays: number;
}

// ---- Availability overrides (task 113e, SRS 12.10.2) ----

export interface SlotAvailabilityOverrideAdminResponse {
  id: string;
  cityId: string;
  cityName: string;
  date: string;
  slotWindowId: string | null;
  slotWindowName: string | null;
  categoryId: string | null;
  categoryName: string | null;
  serviceId: string | null;
  serviceName: string | null;
  reason: string;
}

export interface SlotAvailabilityOverrideCreateRequest {
  cityId: string;
  date: string;
  slotWindowId: string | null;
  categoryId: string | null;
  serviceId: string | null;
  reason: string;
}
