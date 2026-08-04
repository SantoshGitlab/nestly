"use client";

import { useMutation, useQuery, useQueries, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { CitySelector } from "@/components/CitySelector";
import { LocalitySelector } from "@/components/LocalitySelector";
import { STICKY_BAR_SPACER, ScreenSkeleton, StickyActionBar } from "@/components/patterns";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading, Skeleton, Textarea, cx } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { isoDateOffsetFromToday, todayIsoDate } from "@/lib/date";
import type {
  RescheduleEligibilityResponse,
  RescheduleOutcomeResponse,
  SlotAvailability,
} from "@/lib/types";

/**
 * Reschedule flow: eligibility check, city/locality/date/slot picker,
 * confirmation with an immediate booking-detail update (SRS 11.15.3, task
 * 90).
 */
export default function RescheduleBookingPage() {
  return (
    <RequireAuth>
      <RescheduleBookingScreen />
    </RequireAuth>
  );
}

function RescheduleBookingScreen() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { city, locality } = useSelectedCity();

  const [selectedDate, setSelectedDate] = useState<string>(todayIsoDate);
  const [selectedSlotWindowId, setSelectedSlotWindowId] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState<string | null>(null);

  /** Synchronous double-submit guard - see booking/summary/page.tsx. */
  const inFlight = useRef(false);

  const eligibilityQuery = useQuery({
    queryKey: ["reschedule-eligibility", id],
    queryFn: () =>
      apiFetch<RescheduleEligibilityResponse>(`${API_V1}/bookings/${id}/reschedule/eligibility`, {
        authenticated: true,
      }),
  });

  const rescheduleMutation = useMutation({
    mutationFn: () =>
      apiFetch<RescheduleOutcomeResponse>(`${API_V1}/bookings/${id}/reschedule`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({
          localityId: locality!.id,
          slotWindowId: selectedSlotWindowId,
          slotDate: selectedDate,
          reason: reason.trim() ? reason.trim() : null,
        }),
      }),
    onError: () => {
      // Nothing entered is lost - the chosen date, slot and reason are all
      // component state and stay exactly as they were.
      inFlight.current = false;
    },
    onSuccess: async () => {
      // The booking detail page must reflect the new slot immediately.
      await queryClient.invalidateQueries({ queryKey: ["booking", id] });
      router.push(`/bookings/${id}`);
    },
  });

  if (eligibilityQuery.isPending) {
    return <ScreenSkeleton cards={2} className="mx-auto w-full max-w-2xl px-4 py-8 sm:px-6 sm:py-12" />;
  }

  if (eligibilityQuery.isError || !eligibilityQuery.data) {
    return (
      <main className="mx-auto w-full max-w-2xl px-4 py-12 sm:px-6">
        <PageHeading title="Reschedule booking" />
        <Alert
          tone="error"
          title="Couldn't check reschedule eligibility"
          action={
            <Button size="sm" variant="secondary" onClick={() => eligibilityQuery.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(eligibilityQuery.error)}
        </Alert>
      </main>
    );
  }

  const eligibility = eligibilityQuery.data;

  if (!eligibility.isEligible) {
    return (
      <main className="mx-auto w-full max-w-2xl px-4 py-8 sm:px-6 sm:py-12">
        <PageHeading title="Reschedule booking" />
        <Alert tone="warning" title="This booking can't be rescheduled">
          {eligibility.ineligibilityReason ?? "Rescheduling isn't available for this booking."}
        </Alert>
        <p className="nums mt-4 text-sm text-fg-muted">
          Reschedules used: {eligibility.reschedulesUsed} of {eligibility.maxReschedulesPerBooking}
        </p>
        <div className="mt-5">
          <Link
            href={`/bookings/${id}`}
            className="text-sm font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
          >
            Back to booking
          </Link>
        </div>
      </main>
    );
  }

  const submit = () => {
    if (reason.length > 500) {
      setReasonError("Reason must be 500 characters or fewer.");
      return;
    }
    setReasonError(null);
    if (inFlight.current) return;
    inFlight.current = true;
    rescheduleMutation.mutate();
  };

  return (
    <main className={cx("mx-auto w-full max-w-2xl px-4 py-8 sm:px-6 sm:py-12", STICKY_BAR_SPACER)}>
      <PageHeading
        title="Reschedule booking"
        subtitle={`Reschedules used: ${eligibility.reschedulesUsed} of ${eligibility.maxReschedulesPerBooking}`}
      />

      <div className="flex flex-col gap-6">
        <Alert tone="info">
          Your booking stays live until you confirm — pick a new window below and nothing changes
          before then. Moving a slot within{" "}
          <span className="nums">{eligibility.minHoursBeforeSlot}</span> hours of the original may
          count as a late reschedule.
        </Alert>

        <Card title="New slot" description="Pick the locality and date/time you'd like to move to.">
          {city === undefined ? (
            <Skeleton className="h-24 rounded-xl" />
          ) : city === null ? (
            <div className="flex flex-col items-start gap-2.5">
              <p className="text-sm text-fg-muted">Select your city first.</p>
              <CitySelector />
            </div>
          ) : locality === null ? (
            <LocalitySelector cityId={city.id} />
          ) : (
            <RescheduleSlotPicker
              bookingId={id}
              localityId={locality.id}
              selectedDate={selectedDate}
              onDateChange={setSelectedDate}
              selectedSlotWindowId={selectedSlotWindowId}
              onSlotChange={setSelectedSlotWindowId}
            />
          )}
        </Card>

        <Card title="Reason (optional)">
          {/* id="reschedule-reason" is addressed directly by the E2E suite. */}
          <Textarea
            id="reschedule-reason"
            label="Why are you rescheduling?"
            rows={3}
            maxLength={500}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            error={reasonError ?? undefined}
            hint="Helps us hold a better window for you next time."
          />
        </Card>

        {rescheduleMutation.isError ? (
          <Alert tone="error" title="We couldn't move this booking">
            {describeError(rescheduleMutation.error)} Your new slot selection is still here — try
            again.
          </Alert>
        ) : null}

        <StickyActionBar>
          <div className="flex flex-col gap-3 sm:flex-row">
            {/* The accessible name is load-bearing for the E2E suite, so it
                stays constant while the request is in flight. */}
            <Button
              type="button"
              size="lg"
              className="flex-1"
              disabled={!locality || !selectedSlotWindowId}
              loading={rescheduleMutation.isPending}
              onClick={submit}
            >
              Confirm reschedule
            </Button>
            <Button type="button" size="lg" variant="secondary" onClick={() => router.back()}>
              Go back
            </Button>
          </div>
          <p role="status" aria-live="polite" className="sr-only">
            {rescheduleMutation.isPending ? "Moving your booking, please wait." : ""}
          </p>
        </StickyActionBar>
      </div>
    </main>
  );
}

/** How many upcoming days are offered in the date strip - mirrors SlotPicker's VISIBLE_DAYS. */
const VISIBLE_DAYS = 7;

/**
 * Local calendar dates for the strip. Built through lib/date.ts rather than
 * `toISOString().slice(0, 10)`, which converts to UTC first and returns
 * yesterday for every IST morning before 05:30.
 */
function upcomingDates(): string[] {
  return Array.from({ length: VISIBLE_DAYS }, (_, index) => isoDateOffsetFromToday(index));
}

function formatDateLabel(iso: string): { weekday: string; day: string } {
  const date = new Date(`${iso}T00:00:00`);
  return {
    weekday: date.toLocaleDateString(undefined, { weekday: "short" }),
    day: date.toLocaleDateString(undefined, { day: "numeric", month: "short" }),
  };
}

/**
 * Date + slot picker scoped to a single booking's reschedule-slots endpoint
 * (GET /bookings/{bookingId}/reschedule/slots?localityId&date), which is a
 * distinct route from the booking-time GET /slots that SlotPicker.tsx calls.
 * Mirrors SlotPicker's date-strip UX (see that file's doc comment for why
 * every visible date is prefetched) as a dedicated component rather than
 * generalising SlotPicker itself, to avoid any risk of regressing the
 * existing booking flow that depends on it.
 *
 * The `<h3>Date</h3>` immediately followed by a sibling `<div>` of buttons is
 * the shape the E2E suite walks to reach the strip — keep it.
 */
function RescheduleSlotPicker({
  bookingId,
  localityId,
  selectedDate,
  onDateChange,
  selectedSlotWindowId,
  onSlotChange,
}: {
  bookingId: string;
  localityId: string;
  selectedDate: string;
  onDateChange: (date: string) => void;
  selectedSlotWindowId: string | null;
  onSlotChange: (slotWindowId: string | null) => void;
}) {
  // Computed on mount, not via useMemo: see SlotPicker.tsx's identical
  // comment - running `new Date()` and the locale-dependent weekday/day
  // labels below during the server render risks a hydration mismatch
  // whenever the server's default Intl locale differs from the browser's.
  const [dates, setDates] = useState<string[]>([]);
  useEffect(() => setDates(upcomingDates()), []);

  const queries = useQueries({
    queries: dates.map((date) => ({
      queryKey: ["reschedule-slots", bookingId, localityId, date],
      queryFn: () =>
        apiFetch<SlotAvailability>(
          `${API_V1}/bookings/${bookingId}/reschedule/slots?localityId=${localityId}&date=${date}`,
          { authenticated: true },
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
          {dates.length === 0
            ? Array.from({ length: VISIBLE_DAYS }, (_, index) => (
                <Skeleton key={index} className="h-[3.25rem] w-[4.75rem] shrink-0 rounded-xl" />
              ))
            : dates.map((date, index) => {
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
                    title={
                      notServiceable
                        ? "Not available at this address"
                        : knownEmpty
                          ? "Fully booked"
                          : undefined
                    }
                    onClick={() => {
                      onDateChange(date);
                      onSlotChange(null);
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
            {Array.from({ length: 4 }, (_, index) => (
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
            This service isn&apos;t available at this locality.
          </Alert>
        ) : selectedQuery.data.slots.length === 0 ? (
          <Alert tone="info" title="Fully booked">
            No slots left on this date — pick another day from the strip above.
          </Alert>
        ) : (
          <div className="flex flex-wrap gap-2">
            {selectedQuery.data.slots.map((slot) => {
              const isSelected = slot.slotWindowId === selectedSlotWindowId;
              const range = `${slot.startTime.slice(0, 5)}–${slot.endTime.slice(0, 5)}`;
              return (
                <button
                  key={slot.slotWindowId}
                  type="button"
                  aria-pressed={isSelected}
                  onClick={() => onSlotChange(slot.slotWindowId)}
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
        )}
      </div>
    </div>
  );
}
