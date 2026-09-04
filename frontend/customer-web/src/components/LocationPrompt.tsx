"use client";

import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Button, Modal } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch } from "@/lib/api";
import { openCityPicker, setSelectedCity } from "@/lib/location";
import type { City } from "@/lib/types";

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
      const matched = await matchCityToCoordinates(position.coords, citiesQuery.data ?? []);
      if (matched) {
        setSelectedCity(matched);
        setVisible(false);
        return;
      }
      setStatus("no-match");
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

interface GoogleGeocodeAddressComponent {
  long_name: string;
  types: string[];
}

interface GoogleGeocodeResponse {
  results?: Array<{ address_components: GoogleGeocodeAddressComponent[] }>;
}

/**
 * Best-effort GPS -> serviceable-city match via Google's Geocoding API
 * (reusing `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY`, already loaded lazily for the
 * tracking screen - see `lib/googleMaps.ts`'s own note on why it's optional).
 * Resolves to `null` - never throws - on a missing key, a network failure,
 * or a geocoded locality that isn't in `cities`, so every failure mode
 * collapses to the same "fall back to manual" path the caller already has.
 */
async function matchCityToCoordinates(coords: GeolocationCoordinates, cities: City[]): Promise<City | null> {
  const apiKey = process.env.NEXT_PUBLIC_GOOGLE_MAPS_API_KEY;
  if (!apiKey || cities.length === 0) return null;

  try {
    const response = await fetch(
      `https://maps.googleapis.com/maps/api/geocode/json?latlng=${coords.latitude},${coords.longitude}&key=${encodeURIComponent(apiKey)}`,
    );
    if (!response.ok) return null;

    const data = (await response.json()) as GoogleGeocodeResponse;
    const candidateNames = new Set<string>();
    for (const result of data.results ?? []) {
      for (const component of result.address_components) {
        if (component.types.includes("locality") || component.types.includes("administrative_area_level_2")) {
          candidateNames.add(component.long_name.toLowerCase());
        }
      }
    }

    return (
      cities.find((city) => {
        const name = city.name.toLowerCase();
        return Array.from(candidateNames).some((candidate) => candidate.includes(name) || name.includes(candidate));
      }) ?? null
    );
  } catch {
    return null;
  }
}
