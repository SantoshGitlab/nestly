"use client";

import { useMutation, useQuery, useQueries, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { CitySelector } from "@/components/CitySelector";
import { LocalitySelector } from "@/components/LocalitySelector";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { toLocalIsoDate, todayIsoDate } from "@/lib/date";
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
    onSuccess: async () => {
      // The booking detail page must reflect the new slot immediately.
      await queryClient.invalidateQueries({ queryKey: ["booking", id] });
      router.push(`/bookings/${id}`);
    },
  });

  if (eligibilityQuery.isPending) {
    return <main className="mx-auto w-full max-w-2xl px-6 py-12 text-sm text-neutral-500">Loading…</main>;
  }

  if (eligibilityQuery.isError || !eligibilityQuery.data) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <Alert>{describeError(eligibilityQuery.error)}</Alert>
      </main>
    );
  }

  const eligibility = eligibilityQuery.data;

  if (!eligibility.isEligible) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <PageHeading title="Reschedule booking" />
        <Alert>{eligibility.ineligibilityReason ?? "This booking cannot be rescheduled."}</Alert>
        <p className="mt-3 text-sm text-neutral-600 dark:text-neutral-400">
          Reschedules used: {eligibility.reschedulesUsed} of {eligibility.maxReschedulesPerBooking}
        </p>
        <div className="mt-5">
          <Link href={`/bookings/${id}`} className="text-sm font-medium hover:underline">
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
    rescheduleMutation.mutate();
  };

  return (
    <main className="mx-auto w-full max-w-2xl px-6 py-12">
      <PageHeading
        title="Reschedule booking"
        subtitle={`Reschedules used: ${eligibility.reschedulesUsed} of ${eligibility.maxReschedulesPerBooking}`}
      />

      <div className="flex flex-col gap-6">
        <Card title="New slot" description="Pick the locality and date/time you'd like to move to.">
          {city === undefined ? null : city === null ? (
            <div className="flex flex-col items-start gap-2">
              <p className="text-sm text-neutral-600 dark:text-neutral-400">Select your city first.</p>
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
          <div className="flex flex-col gap-1.5">
            <label htmlFor="reschedule-reason" className="text-sm font-medium">
              Why are you rescheduling?
            </label>
            <textarea
              id="reschedule-reason"
              rows={3}
              maxLength={500}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              aria-invalid={reasonError ? true : undefined}
              aria-describedby={reasonError ? "reschedule-reason-error" : undefined}
              className="rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
            />
            {reasonError ? (
              <p id="reschedule-reason-error" className="text-xs text-red-600 dark:text-red-400">
                {reasonError}
              </p>
            ) : null}
          </div>
        </Card>

        {rescheduleMutation.isError ? <Alert>{describeError(rescheduleMutation.error)}</Alert> : null}

        <div className="flex gap-3">
          <Button
            type="button"
            disabled={!locality || !selectedSlotWindowId || rescheduleMutation.isPending}
            onClick={submit}
          >
            {rescheduleMutation.isPending ? "Rescheduling…" : "Confirm reschedule"}
          </Button>
          <Button type="button" variant="secondary" onClick={() => router.back()}>
            Go back
          </Button>
        </div>
      </div>
    </main>
  );
}

/** How many upcoming days are offered in the date strip - mirrors SlotPicker's VISIBLE_DAYS. */
const VISIBLE_DAYS = 7;

const isoDate = toLocalIsoDate;

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
 * Date + slot picker scoped to a single booking's reschedule-slots endpoint
 * (GET /bookings/{bookingId}/reschedule/slots?localityId&date), which is a
 * distinct route from the booking-time GET /slots that SlotPicker.tsx calls.
 * Mirrors SlotPicker's date-strip UX (see that file's doc comment for why
 * every visible date is prefetched) as a dedicated component rather than
 * generalising SlotPicker itself, to avoid any risk of regressing the
 * existing booking flow that depends on it.
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
  const dates = useMemo(upcomingDates, []);

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
    <div className="flex flex-col gap-4">
      <div>
        <h3 className="mb-2 text-sm font-medium">Date</h3>
        <div className="flex gap-2 overflow-x-auto pb-1">
          {dates.map((date, i) => {
            const query = queries[i];
            const { weekday, day } = formatDateLabel(date);
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
                  onSlotChange(null);
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
          <Alert tone="error">This service isn&apos;t available at this locality.</Alert>
        ) : selectedQuery.data.slots.length === 0 ? (
          <p className="text-sm text-neutral-500">No slots available on this date. Try another date.</p>
        ) : (
          <div className="flex flex-wrap gap-2">
            {selectedQuery.data.slots.map((slot) => (
              <button
                key={slot.slotWindowId}
                type="button"
                aria-pressed={slot.slotWindowId === selectedSlotWindowId}
                onClick={() => onSlotChange(slot.slotWindowId)}
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
