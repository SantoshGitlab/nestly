"use client";

import { useQueries } from "@tanstack/react-query";
import { useMemo } from "react";
import { Alert, Button, Skeleton, cx } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { SlotAvailability } from "@/lib/types";

/** How many upcoming days are offered in the date strip (SRS 11.8.2's "available dates"). */
const VISIBLE_DAYS = 7;

/**
 * Local calendar date as YYYY-MM-DD.
 *
 * Deliberately built from the local getFullYear/getMonth/getDate rather than
 * `toISOString().slice(0, 10)`: toISOString converts to UTC first, so for any
 * timezone ahead of UTC the early hours of the morning render as the previous
 * day. In IST (UTC+05:30) that meant every customer opening the picker before
 * 05:30 got a strip starting on yesterday, and "today" silently shifted.
 */
function isoDate(date: Date): string {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function upcomingDates(): string[] {
  const today = new Date();
  return Array.from({ length: VISIBLE_DAYS }, (_, index) => {
    const date = new Date(today);
    date.setDate(date.getDate() + index);
    return isoDate(date);
  });
}

function formatDateLabel(iso: string): { weekday: string; day: string } {
  const date = new Date(`${iso}T00:00:00`);
  return {
    weekday: date.toLocaleDateString(undefined, { weekday: "short" }),
    day: date.toLocaleDateString(undefined, { day: "numeric", month: "short" }),
  };
}

/** Buckets a slot by its start hour so the list reads as a day, not a flat wall of chips. */
const PARTS = [
  { key: "morning", label: "Morning", until: 12 },
  { key: "afternoon", label: "Afternoon", until: 17 },
  { key: "evening", label: "Evening", until: 24 },
] as const;

function partOfDay(startTime: string): (typeof PARTS)[number]["key"] {
  const hour = Number.parseInt(startTime.slice(0, 2), 10);
  return (PARTS.find((part) => hour < part.until) ?? PARTS[PARTS.length - 1]).key;
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
    <div className="flex flex-col gap-6">
      <div>
        <h3 className="mb-2.5 text-sm font-medium text-fg">Date</h3>
        <div className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-2">
          {dates.map((date, index) => {
            const query = queries[index];
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
                // Without this a disabled chip is just faded, leaving the
                // customer to guess why they cannot pick it.
                title={
                  notServiceable
                    ? "Not available at this address"
                    : knownEmpty
                      ? "Fully booked"
                      : undefined
                }
                onClick={() => {
                  onDateChange(date);
                  onSlotChange(null, null);
                }}
                className={cx(
                  "flex min-w-[4.75rem] shrink-0 flex-col items-center gap-0.5 rounded-xl border px-3 py-2.5 text-xs transition duration-fast ease-out",
                  isSelected
                    ? "border-brand-600 bg-brand-600 text-fg-on-brand shadow-brand"
                    : "border-line bg-surface text-fg hover:border-line-strong hover:bg-surface-2",
                  disabled &&
                    "cursor-not-allowed border-line bg-surface-2 text-fg-subtle line-through opacity-60 hover:bg-surface-2",
                )}
              >
                <span className="font-medium">{weekday}</span>
                <span className={cx(isSelected ? "text-fg-on-brand/85" : "text-fg-muted")}>
                  {day}
                </span>
              </button>
            );
          })}
        </div>
      </div>

      <div>
        <h3 className="mb-2.5 text-sm font-medium text-fg">Time window</h3>

        {!selectedQuery || selectedQuery.isPending ? (
          <div className="flex flex-wrap gap-2">
            {Array.from({ length: 6 }, (_, index) => (
              <Skeleton key={index} className="h-11 w-36 rounded-xl" />
            ))}
          </div>
        ) : selectedQuery.isError ? (
          <Alert
            tone="error"
            action={
              <Button size="sm" variant="secondary" onClick={() => selectedQuery.refetch()}>
                Retry
              </Button>
            }
          >
            {describeError(selectedQuery.error)}
          </Alert>
        ) : !selectedQuery.data.isServiceable ? (
          <Alert tone="error" title="Not available here">
            This service isn&apos;t available at this address.
          </Alert>
        ) : selectedQuery.data.slots.length === 0 ? (
          <Alert tone="info" title="Fully booked">
            No slots left on this date — pick another day from the strip above.
          </Alert>
        ) : (
          <div className="flex flex-col gap-4">
            {PARTS.map((part) => {
              const slots = selectedQuery.data.slots.filter(
                (slot) => partOfDay(slot.startTime) === part.key,
              );
              if (slots.length === 0) return null;

              return (
                <div key={part.key}>
                  <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-fg-subtle">
                    {part.label}
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {slots.map((slot) => {
                      const isSelected = slot.slotWindowId === selectedSlotWindowId;
                      const range = `${slot.startTime.slice(0, 5)}–${slot.endTime.slice(0, 5)}`;
                      return (
                        <button
                          key={slot.slotWindowId}
                          type="button"
                          aria-pressed={isSelected}
                          onClick={() => onSlotChange(slot.slotWindowId, `${slot.name} · ${range}`)}
                          className={cx(
                            "flex flex-col items-start rounded-xl border px-3.5 py-2 text-sm transition duration-fast ease-out",
                            isSelected
                              ? "border-brand-600 bg-brand-600 text-fg-on-brand shadow-brand"
                              : "border-line bg-surface text-fg hover:border-line-strong hover:bg-surface-2",
                          )}
                        >
                          <span className="font-medium">{slot.name}</span>
                          <span
                            className={cx(
                              "nums text-xs",
                              isSelected ? "text-fg-on-brand/85" : "text-fg-muted",
                            )}
                          >
                            {range}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
