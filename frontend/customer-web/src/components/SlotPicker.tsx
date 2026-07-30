"use client";

import { useQueries } from "@tanstack/react-query";
import { useMemo } from "react";
import { Alert } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { SlotAvailability } from "@/lib/types";

/** How many upcoming days are offered in the date strip (SRS 11.8.2's "available dates"). */
const VISIBLE_DAYS = 7;

function isoDate(d: Date): string {
  return d.toISOString().slice(0, 10);
}

function upcomingDates(): string[] {
  const today = new Date();
  return Array.from({ length: VISIBLE_DAYS }, (_, i) => {
    const d = new Date(today);
    d.setDate(d.getDate() + i);
    return isoDate(d);
  });
}

function formatDateLabel(iso: string): { weekday: string; day: string } {
  const d = new Date(`${iso}T00:00:00`);
  return {
    weekday: d.toLocaleDateString(undefined, { weekday: "short" }),
    day: d.toLocaleDateString(undefined, { day: "numeric", month: "short" }),
  };
}

/**
 * Date picker + time window selection (SRS 11.8, tasks 63a-c).
 *
 * Every one of the next VISIBLE_DAYS days is fetched up front (GET
 * /slots?serviceId&localityId&date, one call per date) rather than only the
 * selected date - the availability API only ever returns the slots that ARE
 * bookable (cutoff/blackout/advance-window filtering happens server-side, see
 * SlotAvailabilityService), it never returns a disabled slot with a reason.
 * Prefetching lets the date strip itself show which dates have nothing
 * bookable (SRS 11.8.2 "disabled slots"), instead of the customer discovering
 * that only after tapping in.
 *
 * Stale-slot handling (task 63c, SRS 11.8.3 "must fail gracefully if no
 * longer available") is deliberately NOT done here: this component only
 * reflects what the availability list currently says. The authoritative
 * re-check against a stale selection happens at submit time via
 * GET /slots/revalidate, driven by the parent booking summary page.
 */
export function SlotPicker({
  serviceId,
  localityId,
  selectedDate,
  onDateChange,
  selectedSlotWindowId,
  onSlotChange,
}: {
  serviceId: string;
  localityId: string;
  selectedDate: string;
  onDateChange: (date: string) => void;
  selectedSlotWindowId: string | null;
  onSlotChange: (slotWindowId: string | null, slotLabel: string | null) => void;
}) {
  const dates = useMemo(upcomingDates, []);

  const queries = useQueries({
    queries: dates.map((date) => ({
      queryKey: ["slots", serviceId, localityId, date],
      queryFn: () =>
        apiFetch<SlotAvailability>(
          `${API_V1}/slots?serviceId=${serviceId}&localityId=${localityId}&date=${date}`,
        ),
    })),
  });

  const selectedIndex = dates.indexOf(selectedDate);
  const selectedQuery = selectedIndex >= 0 ? queries[selectedIndex] : undefined;

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h3 className="mb-2 text-sm font-medium">Date</h3>
        <div className="flex gap-2 overflow-x-auto pb-1">
          {dates.map((date, i) => {
            const query = queries[i];
            const { weekday, day } = formatDateLabel(date);
            // A date is disabled once we know for certain it has nothing
            // bookable; while still loading (or on a fetch error) it stays
            // selectable rather than guessing.
            const knownEmpty = query.isSuccess && query.data.slots.length === 0;
            const notServiceable = query.isSuccess && !query.data.isServiceable;
            const disabled = knownEmpty || notServiceable;
            const isSelected = date === selectedDate;

            return (
              <button
                key={date}
                type="button"
                disabled={disabled}
                aria-pressed={isSelected}
                onClick={() => {
                  onDateChange(date);
                  onSlotChange(null, null);
                }}
                className={`flex min-w-[4.5rem] flex-col items-center rounded-lg border px-3 py-2 text-xs transition-colors ${
                  isSelected
                    ? "border-black bg-black text-white dark:border-white dark:bg-white dark:text-black"
                    : "border-black/15 hover:bg-black/5 dark:border-white/20 dark:hover:bg-white/10"
                } disabled:cursor-not-allowed disabled:opacity-40`}
              >
                <span className="font-medium">{weekday}</span>
                <span>{day}</span>
              </button>
            );
          })}
        </div>
      </div>

      <div>
        <h3 className="mb-2 text-sm font-medium">Time window</h3>
        {!selectedQuery || selectedQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading time windows…</p>
        ) : selectedQuery.isError ? (
          <Alert>{describeError(selectedQuery.error)}</Alert>
        ) : !selectedQuery.data.isServiceable ? (
          <Alert tone="error">This service isn&apos;t available at this address.</Alert>
        ) : selectedQuery.data.slots.length === 0 ? (
          <p className="text-sm text-neutral-500">No slots available on this date. Try another date.</p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {selectedQuery.data.slots.map((slot) => (
              <button
                key={slot.slotWindowId}
                type="button"
                aria-pressed={slot.slotWindowId === selectedSlotWindowId}
                onClick={() =>
                  onSlotChange(slot.slotWindowId, `${slot.name} · ${slot.startTime.slice(0, 5)}–${slot.endTime.slice(0, 5)}`)
                }
                className={`rounded-lg border px-3 py-2 text-sm transition-colors ${
                  slot.slotWindowId === selectedSlotWindowId
                    ? "border-black bg-black text-white dark:border-white dark:bg-white dark:text-black"
                    : "border-black/15 hover:bg-black/5 dark:border-white/20 dark:hover:bg-white/10"
                }`}
              >
                {slot.name} · {slot.startTime.slice(0, 5)}–{slot.endTime.slice(0, 5)}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
