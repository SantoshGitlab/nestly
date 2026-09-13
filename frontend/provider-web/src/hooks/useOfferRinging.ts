"use client";

import { useEffect, useRef } from "react";
import type { JobListItem } from "@/lib/jobs-types";

/** Chime + vibration cadence. Matches VIBRATION_PATTERN's own total length below, so each cycle's buzz starts right as the previous one's tail pause ends. */
const RING_INTERVAL_MS = 2500;
const VIBRATION_PATTERN = [400, 200, 400, 1500];

function hasUnexpiredOffer(offers: readonly JobListItem[], nowMs: number): boolean {
  // No deadline at all is defensive-only (ProviderJobService always sets one
  // on assignment, per listPendingOffers's own comment) - treated as "not
  // expired" rather than dropped, same as that function's own sort does.
  return offers.some((offer) => !offer.responseDeadline || new Date(offer.responseDeadline).getTime() > nowMs);
}

/**
 * Loops a ringtone + vibration for as long as at least one offer in
 * `offers` is still open (status Assigned, deadline not yet passed),
 * stopping the instant that stops being true - the provider accepted or
 * declined it (it drops out of the list), or its response window ran out.
 *
 * Called once, from `(provider)/layout.tsx` - the authenticated app shell,
 * not any one screen - fed by that layout's own actively-polled `GET /jobs`
 * query. Deliberately shell-level rather than scoped to `/offers`: an offer
 * lands the instant it is assigned regardless of which screen the provider
 * is working from (mid-visit on a different job, checking earnings, ...),
 * so ringing that only fired while `/offers` itself happened to be mounted
 * would miss exactly the cases where a provider most needs the nudge.
 *
 * docs/OPEN-FIXES-FEATURES.csv's "Job offers with countdown" row already
 * closed the "no dedicated offers surface" gap; this closes the next one
 * behind it, that a provider not looking at that screen right now could
 * still miss an offer entirely with nothing audible or physical to notice.
 * It is NOT a substitute for push - it only ever reaches a provider with
 * the app open in a focused tab - see OffersPage's own doc comment for why
 * standing up real push infra (today's server-side provider is a no-op
 * logging sandbox in every environment) is a separate, larger change kept
 * out of this one.
 *
 * Synthesized via the Web Audio API rather than an embedded audio file: a
 * two-tone chime built from two oscillators needs no binary asset, no
 * licensing, and no network fetch to be ready the instant an offer appears.
 *
 * Re-checks on its own timer rather than only when `offers` changes: an
 * offer's deadline can pass with nothing re-rendering this component (no
 * new fetch happened), and a countdown that silently stopped ringing only
 * on the next unrelated refetch would keep buzzing well past "expired".
 *
 * Browsers block audio from starting with no prior user gesture on the
 * page (autoplay policy) - by design, and not something to work around. In
 * practice a provider reaches this shell by signing in, which satisfies it
 * for the rest of that page load; a tab left open with no interaction since
 * load may stay silent for the first offer, same as any other in-page audio
 * would.
 *
 * navigator.vibrate() enforces its own, separate gesture requirement -
 * confirmed against a real Chrome instance: "Blocked call to
 * navigator.vibrate because user hasn't tapped on the frame ... yet" fires
 * even once the audio gesture requirement above is satisfied (e.g.
 * immediately after the sign-in submit that unblocks audio for the rest of
 * the session). A tap anywhere on the resulting page - opening a nav tab is
 * enough - clears it; nothing here works around that either.
 */
export function useOfferRinging(offers: readonly JobListItem[]): void {
  const offersRef = useRef(offers);
  offersRef.current = offers;

  const audioContextRef = useRef<AudioContext | null>(null);
  const shouldRing = hasUnexpiredOffer(offers, Date.now());

  useEffect(() => {
    return () => {
      audioContextRef.current?.close();
    };
  }, []);

  useEffect(() => {
    if (!shouldRing) return;

    const AudioContextClass =
      window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;

    function playChime() {
      if (!AudioContextClass) return;

      // Resume, not (re)create: a context is reused across ring cycles (see
      // the cleanup effect above) and browsers suspend a freshly-created or
      // backgrounded-tab context until something resumes it. A no-op when
      // already running, so safe to call on every cycle.
      const audioContext = audioContextRef.current ?? new AudioContextClass();
      audioContextRef.current = audioContext;
      void audioContext.resume();

      const now = audioContext.currentTime;
      [0, 0.18].forEach((offset, index) => {
        const oscillator = audioContext.createOscillator();
        const gain = audioContext.createGain();
        oscillator.type = "sine";
        oscillator.frequency.value = index === 0 ? 880 : 1108.73; // A5, then C#6 - a bright two-note chime, not one harsh tone
        gain.gain.setValueAtTime(0, now + offset);
        gain.gain.linearRampToValueAtTime(0.25, now + offset + 0.02);
        gain.gain.linearRampToValueAtTime(0, now + offset + 0.16);
        oscillator.connect(gain);
        gain.connect(audioContext.destination);
        oscillator.start(now + offset);
        oscillator.stop(now + offset + 0.16);
      });

      if ("vibrate" in navigator) {
        navigator.vibrate(VIBRATION_PATTERN);
      }
    }

    playChime();
    const intervalId = setInterval(() => {
      // offersRef, not the closed-over `offers`: this timer must keep
      // reading the latest list across the whole open-ended ringing window,
      // not just the snapshot from whichever render started it.
      if (!hasUnexpiredOffer(offersRef.current, Date.now())) {
        clearInterval(intervalId);
        return;
      }
      playChime();
    }, RING_INTERVAL_MS);

    return () => {
      clearInterval(intervalId);
      if ("vibrate" in navigator) {
        navigator.vibrate(0); // cancels any in-flight pattern
      }
    };
  }, [shouldRing]);
}
