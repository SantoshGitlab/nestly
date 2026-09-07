"use client";

import type { City } from "./types";

/**
 * Client-side location selection (SRS 11.1, 11.4.1).
 *
 * localStorage rather than auth.ts's sessionStorage: a chosen city is a
 * browsing preference, not a credential, so it should survive the browser
 * being closed and reopened rather than dying with the tab.
 */
const CITY_KEY = "nestly.city";
const LOCALITY_KEY = "nestly.locality";
/**
 * The raw, human-readable address GPS auto-detect resolved (e.g. "Genus
 * Power Infrastructures Ltd, SPL3, Sitapura") - cosmetic only, shown in the
 * header pill in place of "City - Area" so a customer whose real area isn't
 * a seeded serviceable locality still sees where they actually are, not
 * just the city. Deliberately separate from `SelectedLocality`: that type
 * drives real catalog/serviceability filtering and must only ever hold a
 * real seeded locality, never an arbitrary geocoded string. Cleared by every
 * manual city/area pick (see `setSelectedCity`/`setSelectedLocality` below)
 * so a stale detected address never survives the customer overriding it.
 */
const DETECTED_ADDRESS_KEY = "nestly.detectedAddress";

/** Notifies subscribed components (city selector, serviceability checks) that location moved. */
const LOCATION_CHANGED_EVENT = "nestly:location-changed";

/**
 * Lets any component ask the header's `CitySelector` to open its picker
 * modal without owning that modal's state itself - `LocationPrompt` uses
 * this for its "choose manually" escape hatch and its own no-match fallback,
 * so the manual picker stays defined in exactly one place.
 */
const OPEN_CITY_PICKER_EVENT = "nestly:open-city-picker";

export interface SelectedLocality {
  id: string;
  name: string;
  pincodeId: string;
}

function isBrowser(): boolean {
  return typeof window !== "undefined";
}

export function getSelectedCity(): City | null {
  if (!isBrowser()) return null;
  const raw = localStorage.getItem(CITY_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as City;
  } catch {
    return null;
  }
}

export function setSelectedCity(city: City): void {
  if (!isBrowser()) return;
  localStorage.setItem(CITY_KEY, JSON.stringify(city));
  // A locality from a previously selected city is meaningless once the city
  // changes - clear it so a stale pincode never silently backs a
  // serviceability or slot check against the wrong city.
  localStorage.removeItem(LOCALITY_KEY);
  localStorage.removeItem(DETECTED_ADDRESS_KEY);
  window.dispatchEvent(new Event(LOCATION_CHANGED_EVENT));
}

export function getSelectedLocality(): SelectedLocality | null {
  if (!isBrowser()) return null;
  const raw = localStorage.getItem(LOCALITY_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as SelectedLocality;
  } catch {
    return null;
  }
}

export function setSelectedLocality(locality: SelectedLocality): void {
  if (!isBrowser()) return;
  localStorage.setItem(LOCALITY_KEY, JSON.stringify(locality));
  localStorage.removeItem(DETECTED_ADDRESS_KEY);
  window.dispatchEvent(new Event(LOCATION_CHANGED_EVENT));
}

export function clearSelectedLocality(): void {
  if (!isBrowser()) return;
  localStorage.removeItem(LOCALITY_KEY);
  localStorage.removeItem(DETECTED_ADDRESS_KEY);
  window.dispatchEvent(new Event(LOCATION_CHANGED_EVENT));
}

/**
 * Cosmetic-only detected-address text (see `DETECTED_ADDRESS_KEY`). Set by
 * `LocationPrompt` after its own `setSelectedCity`/`setSelectedLocality`
 * calls (both of which clear this key), so GPS auto-detect's raw text always
 * wins over those calls' own clearing side effect. Never read by anything
 * that drives serviceability - `getSelectedLocality` remains the only
 * source of truth for that.
 */
export function getDetectedAddressLabel(): string | null {
  if (!isBrowser()) return null;
  return localStorage.getItem(DETECTED_ADDRESS_KEY);
}

export function setDetectedAddressLabel(label: string): void {
  if (!isBrowser()) return;
  localStorage.setItem(DETECTED_ADDRESS_KEY, label);
  window.dispatchEvent(new Event(LOCATION_CHANGED_EVENT));
}

export function subscribeToLocationChanges(listener: () => void): () => void {
  if (!isBrowser()) return () => undefined;
  window.addEventListener(LOCATION_CHANGED_EVENT, listener);
  return () => window.removeEventListener(LOCATION_CHANGED_EVENT, listener);
}

export function openCityPicker(): void {
  if (!isBrowser()) return;
  window.dispatchEvent(new Event(OPEN_CITY_PICKER_EVENT));
}

export function subscribeToOpenCityPicker(listener: () => void): () => void {
  if (!isBrowser()) return () => undefined;
  window.addEventListener(OPEN_CITY_PICKER_EVENT, listener);
  return () => window.removeEventListener(OPEN_CITY_PICKER_EVENT, listener);
}
