"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { BookingDetail, ServiceDetail } from "@/lib/types";

/**
 * Booking confirmation page (SRS 11.12.3, tasks 64a-d): booking ID, summary,
 * guidance, and a link into the full detail page.
 *
 * Wrapped in Suspense for useSearchParams (see search/page.tsx).
 */
export default function BookingSuccessPage() {
  return (
    <Suspense fallback={<main className="mx-auto w-full max-w-2xl px-6 py-12" />}>
      <RequireAuth>
        <BookingSuccessScreen />
      </RequireAuth>
    </Suspense>
  );
}

function BookingSuccessScreen() {
  const { id } = useParams<{ id: string }>();
  // Set by the booking summary page's redirect so this page can show the
  // real cancellation/reschedule policy text - BookingDetailResponse itself
  // doesn't carry policy fields (only BookingSummaryResponse does), so
  // without this the guidance section would have to fabricate the wording.
  const serviceSlug = useSearchParams().get("serviceSlug");

  const bookingQuery = useQuery({
    queryKey: ["booking", id],
    queryFn: () => apiFetch<BookingDetail>(`${API_V1}/bookings/${id}`, { authenticated: true }),
  });

  const policyQuery = useQuery({
    queryKey: ["service", serviceSlug],
    queryFn: () => apiFetch<ServiceDetail>(`${API_V1}/services/${serviceSlug}`),
    enabled: !!serviceSlug,
  });

  if (bookingQuery.isPending) {
    return <main className="mx-auto w-full max-w-2xl px-6 py-12 text-sm text-neutral-500">Loading…</main>;
  }

  if (bookingQuery.isError || !bookingQuery.data) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <Alert>{describeError(bookingQuery.error)}</Alert>
      </main>
    );
  }

  const booking = bookingQuery.data;

  return (
    <main className="mx-auto w-full max-w-2xl px-6 py-12">
      <div className="mb-6 flex flex-col items-center gap-2 text-center">
        <span className="text-4xl" aria-hidden="true">
          ✅
        </span>
        <PageHeading title="Booking placed!" subtitle={`Booking ID: ${booking.id}`} />
      </div>

      <Card title="Booking summary">
        <dl className="flex flex-col gap-2 text-sm">
          <Row label="Service" value={booking.service.name} />
          <Row label="Date" value={booking.slot.date} />
          <Row
            label="Time"
            value={`${booking.slot.name} · ${booking.slot.startTime.slice(0, 5)}–${booking.slot.endTime.slice(0, 5)}`}
          />
          <Row
            label="Address"
            value={`${booking.address.line1}, ${booking.address.city} ${booking.address.pincode}`}
          />
          <Row label="Status" value={booking.statusLabel} />
          <div className="mt-1 flex items-center justify-between border-t border-black/10 pt-2 font-semibold dark:border-white/15">
            <dt>Amount payable</dt>
            <dd>₹{booking.price.totalPayable.toFixed(2)}</dd>
          </div>
        </dl>
      </Card>

      <div className="mt-6 flex flex-col gap-3 rounded-xl border border-black/10 bg-white p-5 text-sm text-neutral-600 dark:border-white/15 dark:bg-neutral-900 dark:text-neutral-400">
        <p className="font-medium text-neutral-900 dark:text-neutral-100">What happens next</p>
        <p>
          We&apos;ll assign a professional for your slot and keep you updated on the status of this
          booking. You can track progress anytime from your bookings page.
        </p>
        {policyQuery.data?.cancellationPolicy || policyQuery.data?.reschedulePolicy ? (
          <div>
            <p className="mb-1 font-medium text-neutral-900 dark:text-neutral-100">
              Cancellation &amp; rescheduling
            </p>
            {policyQuery.data.cancellationPolicy ? <p>{policyQuery.data.cancellationPolicy}</p> : null}
            {policyQuery.data.reschedulePolicy ? <p>{policyQuery.data.reschedulePolicy}</p> : null}
          </div>
        ) : null}
      </div>

      <div className="mt-6 flex flex-wrap gap-3">
        <Link href={`/bookings/${booking.id}`}>
          <Button type="button">View booking details</Button>
        </Link>
        <Link href="/bookings">
          <Button type="button" variant="secondary">
            Go to my bookings
          </Button>
        </Link>
      </div>
    </main>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4 text-neutral-600 dark:text-neutral-400">
      <dt>{label}</dt>
      <dd className="text-right text-neutral-900 dark:text-neutral-100">{value}</dd>
    </div>
  );
}
