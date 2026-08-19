"use client";

import { useEffect, useRef, useState } from "react";
import { loadGoogleMaps } from "@/lib/googleMaps";

/**
 * Live provider-and-destination map for the tracking screen (task 280,
 * consumed by task 281). Loads the Maps JS API lazily via
 * {@link loadGoogleMaps} - nothing is requested until this component actually
 * mounts, which only happens on the tracking screen itself.
 *
 * Renders nothing (a styled placeholder, not an empty box) when the API key
 * is absent or the script fails to load, so the screen's status/ETA panel is
 * fully usable without a map in local dev and CI. The caller decides what
 * "without a map" looks like around this component; this component only
 * decides what its own footprint looks like.
 */
export function TrackingMap({
  providerLocation,
  destination,
  className,
}: {
  providerLocation: { latitude: number; longitude: number } | null;
  destination: { latitude: number; longitude: number };
  className?: string;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<google.maps.Map | null>(null);
  const providerMarkerRef = useRef<google.maps.marker.AdvancedMarkerElement | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "unavailable">("loading");

  // One-time setup: create the map and the destination pin once the API is
  // available. Split from the position-update effect below so panning the
  // provider marker on every fix does not tear the map down and rebuild it.
  useEffect(() => {
    let cancelled = false;

    loadGoogleMaps().then((maps) => {
      if (cancelled) return;
      if (!maps || !containerRef.current) {
        setStatus("unavailable");
        return;
      }

      const map = new maps.Map(containerRef.current, {
        center: { lat: destination.latitude, lng: destination.longitude },
        zoom: 14,
        disableDefaultUI: true,
        zoomControl: true,
        mapId: "nestly-tracking",
        // Explicit rather than relying on the SDK's own "auto" default
        // (which happens to resolve to this on touch devices today): a
        // one-finger drag starting on the map scrolls the page instead of
        // panning it, and a "use two fingers to move the map" hint appears
        // on the first touch attempt instead - the map never traps a phone
        // scroll gesture (task #352's one-handed-usability pass).
        gestureHandling: "cooperative",
      });
      mapRef.current = map;

      new maps.marker.AdvancedMarkerElement({
        map,
        position: { lat: destination.latitude, lng: destination.longitude },
        content: buildPinElement("destination"),
        title: "Your address",
      });

      setStatus("ready");
    });

    return () => {
      cancelled = true;
      mapRef.current = null;
      providerMarkerRef.current = null;
    };
    // destination is the booking's immutable address snapshot (see
    // TrackedDestination's doc comment on the backend) - it cannot change
    // under a mounted tracking screen, so this intentionally does not
    // re-run the map setup when it "changes" (it never does).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Provider position updates: create the marker on the first fix, then move
  // it - CSS handles the glide (see the marker's own transition), so a
  // position update never teleports.
  useEffect(() => {
    const map = mapRef.current;
    if (!map || !providerLocation) return;

    const position = { lat: providerLocation.latitude, lng: providerLocation.longitude };

    if (!providerMarkerRef.current) {
      loadGoogleMaps().then((maps) => {
        if (!maps || !mapRef.current) return;
        providerMarkerRef.current = new maps.marker.AdvancedMarkerElement({
          map: mapRef.current,
          position,
          content: buildPinElement("provider"),
          title: "Your professional",
        });
      });
      return;
    }

    providerMarkerRef.current.position = position;
  }, [providerLocation?.latitude, providerLocation?.longitude]);

  if (status === "unavailable") {
    return (
      <div
        className={
          "flex min-h-[12rem] flex-col items-center justify-center gap-2 rounded-2xl border border-line bg-surface-2 px-4 py-8 text-center " +
          (className ?? "")
        }
      >
        <MapPlaceholderIcon />
        <p className="text-sm font-medium text-fg">Live map unavailable</p>
        <p className="max-w-xs text-xs text-fg-subtle">
          We&apos;ll keep updating the status and ETA below without it.
        </p>
      </div>
    );
  }

  return (
    <div className={"relative min-h-[12rem] overflow-hidden rounded-2xl border border-line " + (className ?? "")}>
      {status === "loading" ? (
        <div className="absolute inset-0 z-10 animate-pulse bg-surface-2" aria-hidden />
      ) : null}
      <div ref={containerRef} className="h-full min-h-[12rem] w-full" />
    </div>
  );
}

/**
 * A small coloured pin built from CSS-variable design tokens rather than
 * hard-coded colours, per task 280 - brand for the provider (matches the CTA
 * colour everywhere else in the app), fg-muted for the fixed destination so
 * the two are never confusable at a glance. Position transitions with a CSS
 * `transition`, not a Maps animation API, so the provider marker glides
 * between fixes instead of jumping (task 281's requirement, set up for here
 * since the element itself is created here).
 */
function buildPinElement(kind: "provider" | "destination"): HTMLElement {
  const pin = document.createElement("div");
  pin.style.width = kind === "provider" ? "1.25rem" : "1rem";
  pin.style.height = pin.style.width;
  pin.style.borderRadius = "9999px";
  pin.style.border = "2px solid rgb(var(--surface))";
  pin.style.boxShadow = "0 1px 4px rgb(var(--fg) / 0.35)";
  pin.style.transition = "transform 0.6s ease-out";
  pin.style.background =
    kind === "provider" ? "rgb(var(--brand-600))" : "rgb(var(--fg-muted))";
  return pin;
}

function MapPlaceholderIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="h-8 w-8 text-fg-subtle"
      aria-hidden
    >
      <path d="M9 20l-5.447-2.724A1 1 0 0 1 3 16.382V5.618a1 1 0 0 1 1.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0 0 21 18.382V7.618a1 1 0 0 0-.553-.894L15 4m0 13V4m0 0L9 7" />
    </svg>
  );
}
