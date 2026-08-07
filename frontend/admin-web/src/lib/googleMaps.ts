/**
 * Lazy client-side loader for the Google Maps JavaScript API (task 280).
 *
 * Nothing here runs at import time - no `<script>` tag is injected until
 * {@link loadGoogleMaps} is actually called, which only the tracking screen
 * (task 281) does. That is the point: this API is billed per load, and a tag
 * on every first paint would put it in the critical path of pages (search,
 * category, booking) that never show a map.
 *
 * `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY` is optional. When it is unset -
 * local dev and CI have no billing account and never will - this resolves to
 * `null` instead of throwing, so a caller renders its no-map fallback rather
 * than crashing the tracking screen over a missing map.
 */

declare global {
  interface Window {
    google?: typeof google;
  }
}

let loadPromise: Promise<typeof google.maps | null> | null = null;

/**
 * Loads the Maps JS API's `maps` library exactly once per page load, however
 * many components ask for it - later callers get the same in-flight/resolved
 * promise rather than a second `<script>` tag.
 */
export function loadGoogleMaps(): Promise<typeof google.maps | null> {
  if (loadPromise) return loadPromise;

  const apiKey = process.env.NEXT_PUBLIC_GOOGLE_MAPS_API_KEY;
  if (!apiKey) {
    loadPromise = Promise.resolve(null);
    return loadPromise;
  }

  if (window.google?.maps) {
    loadPromise = Promise.resolve(window.google.maps);
    return loadPromise;
  }

  loadPromise = new Promise<typeof google.maps | null>((resolve, reject) => {
    const callbackName = "__nestlyGoogleMapsLoaded";
    (window as unknown as Record<string, () => void>)[callbackName] = () => {
      resolve(window.google?.maps ?? null);
    };

    const script = document.createElement("script");
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=marker&callback=${callbackName}&loading=async`;
    script.async = true;
    script.onerror = () => reject(new Error("Failed to load the Google Maps script."));
    document.head.appendChild(script);
  }).catch((): typeof google.maps | null => {
    // A network failure or an invalid/restricted key degrades to the same
    // no-map fallback as an absent key - the tracking screen has one failure
    // mode for "no map available", not two.
    return null;
  });

  return loadPromise;
}
