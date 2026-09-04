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
  window.dispatchEvent(new Event(LOCATION_CHANGED_EVENT));
}

export function clearSelectedLocality(): void {
  if (!isBrowser()) return;
  localStorage.removeItem(LOCALITY_KEY);
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
