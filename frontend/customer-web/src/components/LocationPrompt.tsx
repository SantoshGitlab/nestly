"use client";

import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Button, Modal } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch } from "@/lib/api";
import { openCityPicker, setSelectedCity, setSelectedLocality } from "@/lib/location";
import type { City, LocalitySearchResult } from "@/lib/types";

const PROMPTED_KEY = "nestly.locationPrompted";
const MOBILE_QUERY = "(max-width: 767px)";

/**
 * One-time-per-session mobile prompt to enable device location on first
 * load of the home page, so it can auto-select the customer's city instead
 * of leaving "Select city" showing until they notice and tap it themselves.
 *
 * Desktop never sees this: a location dialog on a pointer-driven session
 * reads as an ad-tech dark pattern rather than a convenience, and nothing
 * about browsing from a laptop implies "where I am right now" the way
 * opening a phone does. Gated by viewport width via `matchMedia`, matching
 * the breakpoint the rest of the shell already treats as "mobile" (see
 * `BottomTabBar`), not by user-agent sniffing.
 *
 * Shows a soft explainer first rather than calling `getCurrentPosition`
 * cold: the native browser permission dialog gives the customer no context,
 * and a reflexive "Block" there takes a manual browser-settings change to
 * undo - a mistake this app can't recover from with a second ask. Choosing
 * "Allow location" here is what triggers the real permission prompt, so it
 * only ever appears after the customer has already agreed once.
 *
 * "Choose manually" - and the fallback when detection succeeds but resolves
 * to a city Glavyx doesn't serve yet - both hand off to the header's
 * existing `CitySelector` via `openCityPicker()` rather than re-implementing
 * a second city list here.
 */
export function LocationPrompt() {
  const { city } = useSelectedCity();
  const [isMobile, setIsMobile] = useState(false);
  const [visible, setVisible] = useState(false);
  const [status, setStatus] = useState<"idle" | "locating" | "no-match" | "unsupported">("idle");

  useEffect(() => {
    const query = window.matchMedia(MOBILE_QUERY);
    setIsMobile(query.matches);
    const onChange = (event: MediaQueryListEvent) => setIsMobile(event.matches);
    query.addEventListener("change", onChange);
    return () => query.removeEventListener("change", onChange);
  }, []);

  useEffect(() => {
    if (!isMobile) return;
    if (city !== null) return; // undefined = still reading storage, a City = already chosen - neither should be interrupted
    if (sessionStorage.getItem(PROMPTED_KEY)) return;
    // Marked as soon as the prompt is shown, not on a choice being made -
    // dismissing (Escape, backdrop click) still counts as "already asked
    // this session" so a refresh can't turn this into a nag.
    sessionStorage.setItem(PROMPTED_KEY, "1");
    setVisible(true);
  }, [isMobile, city]);

  const citiesQuery = useQuery({
    queryKey: ["geography", "cities"],
    queryFn: () => apiFetch<City[]>(`${API_V1}/geography/cities`),
    enabled: visible,
  });

  function chooseManually() {
    setVisible(false);
    openCityPicker();
  }

  async function allow() {
    if (!navigator.geolocation) {
      setStatus("unsupported");
      return;
    }

    setStatus("locating");
    try {
      const position = await new Promise<GeolocationPosition>((resolve, reject) => {
        navigator.geolocation.getCurrentPosition(resolve, reject, { timeout: 8000 });
      });
      const address = await reverseGeocode(position.coords);
      const matchedCity = address ? matchCity(address, citiesQuery.data ?? []) : null;
      if (!matchedCity) {
        setStatus("no-match");
        return;
      }

      setSelectedCity(matchedCity);
      setVisible(false);

      // Best-effort only, and deliberately after closing the dialog: the
      // customer's city is already resolved and usable, so a slow or failed
      // area lookup must never leave them staring at a spinner over what
      // already succeeded.
      try {
        const localities = await apiFetch<LocalitySearchResult[]>(
          `${API_V1}/geography/cities/${matchedCity.id}/localities`,
        );
        const matchedLocality = address ? matchLocality(address, localities) : null;
        if (matchedLocality) {
          setSelectedLocality({
            id: matchedLocality.id,
            name: matchedLocality.name,
            pincodeId: matchedLocality.pincodeId,
          });
        }
      } catch {
        // City alone is still a fully usable selection - see comment above.
      }
    } catch {
      setStatus("no-match");
    }
  }

  if (!visible) return null;

  const busy = status === "locating";

  return (
    <Modal open={visible} onClose={() => setVisible(false)} title="Enable your location" size="sm">
      <div className="flex flex-col gap-4">
        <p className="text-sm text-fg-muted">
          {status === "no-match"
            ? "We couldn't match that to a city we serve yet - pick one manually instead."
            : status === "unsupported"
              ? "Your browser doesn't support location access here - pick a city manually instead."
              : "Allow location access so we can show services available near you."}
        </p>
        <div className="flex flex-col gap-2">
          {status !== "unsupported" && (
            <Button fullWidth loading={busy} disabled={busy} onClick={allow}>
              Allow location
            </Button>
          )}
          <Button fullWidth variant="secondary" disabled={busy} onClick={chooseManually}>
            Choose city manually
          </Button>
        </div>
      </div>
    </Modal>
  );
}

interface NominatimAddress {
  city?: string;
  town?: string;
  village?: string;
  county?: string;
  state_district?: string;
  neighbourhood?: string;
  suburb?: string;
  quarter?: string;
  city_district?: string;
  postcode?: string;
}

interface NominatimReverseResponse {
  address?: NominatimAddress;
}

/**
 * Reverse-geocodes a GPS fix into an OpenStreetMap address breakdown via
 * Nominatim. Deliberately not Google's Geocoding API: that requires a billed
 * API key (`NEXT_PUBLIC_GOOGLE_MAPS_API_KEY`, which this app also uses -
 * optionally - for the tracking screen's map tiles, see `lib/googleMaps.ts`),
 * so a customer with no key configured could never get a match here
 * regardless of permission being granted. Nominatim needs no key and no
 * billing account for this call volume (one request per customer who opts
 * in, at most once a session). `zoom=18` (street level) is requested rather
 * than a coarser zoom so the same single response carries both the city-
 * level fields `matchCity` needs and the neighbourhood/suburb-level fields
 * `matchLocality` needs - one network round trip serves both matches.
 * Resolves to `null` - never throws - on any network failure, collapsing
 * into the same "fall back to manual" path every other failure mode uses.
 */
async function reverseGeocode(coords: GeolocationCoordinates): Promise<NominatimAddress | null> {
  try {
    const response = await fetch(
      `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${coords.latitude}&lon=${coords.longitude}&zoom=18&addressdetails=1`,
    );
    if (!response.ok) return null;

    const data = (await response.json()) as NominatimReverseResponse;
    return data.address ?? null;
  } catch {
    return null;
  }
}

function namesMatch(candidate: string, name: string): boolean {
  return candidate.includes(name) || name.includes(candidate);
}

/** Matches a reverse-geocoded address against Glavyx's serviceable cities. */
function matchCity(address: NominatimAddress, cities: City[]): City | null {
  if (cities.length === 0) return null;

  const candidateNames = [address.city, address.town, address.village, address.county, address.state_district]
    .filter((value): value is string => Boolean(value))
    .map((value) => value.toLowerCase());

  return cities.find((city) => candidateNames.some((candidate) => namesMatch(candidate, city.name.toLowerCase()))) ?? null;
}

/**
 * Matches a reverse-geocoded address against the admin-seeded areas within
 * one already-matched city. Matched by area name first, not postcode:
 * OpenStreetMap's crowd-sourced `postcode` tagging in India is often
 * imprecise or street-level rather than the official India Post PIN (spot-
 * checked against this app's own seed data - the same coordinates that
 * clearly sit inside a seeded "Mansarovar" area came back tagged with a
 * different postcode than that area's seeded PIN code), so requiring an
 * exact postcode match would silently miss real matches. A postcode match is
 * still accepted as an alternate signal, since it costs nothing when it
 * happens to line up. No match (customer is inside a serviceable city but
 * an area Glavyx hasn't onboarded yet) is a normal outcome, not a failure -
 * the caller leaves the city-only selection in place rather than inventing
 * an unserviceable area.
 */
function matchLocality(address: NominatimAddress, localities: LocalitySearchResult[]): LocalitySearchResult | null {
  if (localities.length === 0) return null;

  const candidateNames = [address.neighbourhood, address.suburb, address.quarter, address.city_district]
    .filter((value): value is string => Boolean(value))
    .map((value) => value.toLowerCase());

  return (
    localities.find((locality) => {
      if (address.postcode && address.postcode === locality.pincodeCode) return true;
      const name = locality.name.toLowerCase();
      return candidateNames.some((candidate) => namesMatch(candidate, name));
    }) ?? null
  );
}
