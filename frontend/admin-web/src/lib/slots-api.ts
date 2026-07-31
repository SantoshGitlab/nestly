/**
 * Typed client for the Admin API's slot configuration surface (SRS 12.10,
 * tasks 113a-e): `SlotsController`. Every call is authenticated - these are
 * admin-only endpoints gated behind the "slots" permission module server-side.
 */
import { API_V1, apiFetch } from "./api";
import type {
  CategoryLookupResponse,
  ServiceLookupResponse,
  SlotAvailabilityOverrideAdminResponse,
  SlotAvailabilityOverrideCreateRequest,
  SlotBlackoutAdminResponse,
  SlotBlackoutCreateRequest,
  SlotBookingPolicyAdminResponse,
  SlotBookingPolicyUpsertRequest,
  SlotCityLookupResponse,
  SlotWindowAdminResponse,
  SlotWindowCapacityUpdateRequest,
  SlotWindowCreateRequest,
  SlotWindowUpdateRequest,
} from "./slots-types";

const SLOTS_BASE = `${API_V1}/slots`;

function query(params: Record<string, string | undefined>): string {
  const entries = Object.entries(params).filter(([, value]) => value !== undefined);
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries as [string, string][]).toString()}`;
}

// ---- Lookups ----

export const listSlotCityLookups = () =>
  apiFetch<SlotCityLookupResponse[]>(`${SLOTS_BASE}/cities`, { authenticated: true });

export const listSlotCategoryLookups = () =>
  apiFetch<CategoryLookupResponse[]>(`${SLOTS_BASE}/categories`, { authenticated: true });

export const listSlotServiceLookups = () =>
  apiFetch<ServiceLookupResponse[]>(`${SLOTS_BASE}/services`, { authenticated: true });

// ---- Windows (task 113a) ----

export const listSlotWindows = (cityId?: string) =>
  apiFetch<SlotWindowAdminResponse[]>(`${SLOTS_BASE}/windows${query({ cityId })}`, { authenticated: true });

export const createSlotWindow = (request: SlotWindowCreateRequest) =>
  apiFetch<SlotWindowAdminResponse>(`${SLOTS_BASE}/windows`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateSlotWindow = (id: string, request: SlotWindowUpdateRequest) =>
  apiFetch<SlotWindowAdminResponse>(`${SLOTS_BASE}/windows/${id}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

// ---- Capacity (task 113d) ----

export const setSlotWindowCapacity = (id: string, request: SlotWindowCapacityUpdateRequest) =>
  apiFetch<SlotWindowAdminResponse>(`${SLOTS_BASE}/windows/${id}/capacity`, {
    method: "PATCH",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const setSlotWindowActive = (id: string, isActive: boolean) =>
  apiFetch<void>(`${SLOTS_BASE}/windows/${id}/${isActive ? "activate" : "deactivate"}`, {
    method: "POST",
    authenticated: true,
  });

// ---- Blackouts (task 113b) ----

export const listSlotBlackouts = (cityId?: string) =>
  apiFetch<SlotBlackoutAdminResponse[]>(`${SLOTS_BASE}/blackouts${query({ cityId })}`, { authenticated: true });

export const createSlotBlackout = (request: SlotBlackoutCreateRequest) =>
  apiFetch<SlotBlackoutAdminResponse>(`${SLOTS_BASE}/blackouts`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const deleteSlotBlackout = (id: string) =>
  apiFetch<void>(`${SLOTS_BASE}/blackouts/${id}`, { method: "DELETE", authenticated: true });

// ---- Cutoffs / advance-booking policy (task 113c) ----

export const listSlotBookingPolicies = () =>
  apiFetch<SlotBookingPolicyAdminResponse[]>(`${SLOTS_BASE}/booking-policies`, { authenticated: true });

export const upsertSlotBookingPolicy = (request: SlotBookingPolicyUpsertRequest) =>
  apiFetch<SlotBookingPolicyAdminResponse>(`${SLOTS_BASE}/booking-policies`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

// ---- Availability overrides (task 113e, SRS 12.10.2) ----

export const listSlotAvailabilityOverrides = (cityId?: string, date?: string) =>
  apiFetch<SlotAvailabilityOverrideAdminResponse[]>(`${SLOTS_BASE}/overrides${query({ cityId, date })}`, {
    authenticated: true,
  });

export const createSlotAvailabilityOverride = (request: SlotAvailabilityOverrideCreateRequest) =>
  apiFetch<SlotAvailabilityOverrideAdminResponse>(`${SLOTS_BASE}/overrides`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const deleteSlotAvailabilityOverride = (id: string) =>
  apiFetch<void>(`${SLOTS_BASE}/overrides/${id}`, { method: "DELETE", authenticated: true });
