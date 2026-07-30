"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { BookingStatus } from "@/lib/types";
import type { BookingDetail, BookingStatusTimelineEntry, PriceBreakdown } from "@/lib/types";

/** Both reachable only via RefundPending/Refunded in BookingLifecycle - see BookingStatusMapper.cs. */
const REFUND_STATUSES: BookingStatus[] = [BookingStatus.RefundPending, BookingStatus.Refunded];

/**
 * Booking detail with status timeline, refund info, and action CTAs (SRS
 * 11.13.2, tasks 65a-c).
 */
export default function BookingDetailPage() {
  return (
    <RequireAuth>
      <BookingDetailScreen />
    </RequireAuth>
  );
}

function BookingDetailScreen() {
  const { id } = useParams<{ id: string }>();

  const query = useQuery({
    queryKey: ["booking", id],
    queryFn: () => apiFetch<BookingDetail>(`${API_V1}/bookings/${id}`, { authenticated: true }),
  });

  if (query.isPending) {
    return <main className="mx-auto w-full max-w-3xl px-6 py-12 text-sm text-neutral-500">Loading…</main>;
  }

  if (query.isError || !query.data) {
    return (
      <main className="mx-auto w-full max-w-3xl px-6 py-12">
        <Alert>{describeError(query.error)}</Alert>
      </main>
    );
  }

  const booking = query.data;

  return (
    <main className="mx-auto grid w-full max-w-4xl gap-6 px-6 py-10 md:grid-cols-[1fr_320px]">
      <div className="flex flex-col gap-6">
        <PageHeading title={booking.service.name} subtitle={`Booking ID: ${booking.id}`} />

        <Card title="Address">
          <address className="text-sm not-italic leading-relaxed text-neutral-700 dark:text-neutral-300">
            {booking.address.line1}
            {booking.address.line2 ? <>, {booking.address.line2}</> : null}
            {booking.address.landmark ? <>, near {booking.address.landmark}</> : null}
            <br />
            {booking.address.city}, {booking.address.state} {booking.address.pincode}
            <br />
            {booking.address.contactName} · {booking.address.contactMobile}
          </address>
        </Card>

        <Card title="Slot">
          <p className="text-sm">
            {booking.slot.date} · {booking.slot.name} ({booking.slot.startTime.slice(0, 5)}–
            {booking.slot.endTime.slice(0, 5)})
          </p>
        </Card>

        <Card title="Price & payment">
          <PriceRows breakdown={booking.price} />
        </Card>

        {REFUND_STATUSES.includes(booking.status) ? <RefundInfoCard booking={booking} /> : null}

        <Card title="Status timeline">
          <Timeline entries={booking.timeline} />
        </Card>
      </div>

      <aside className="flex flex-col gap-3 md:sticky md:top-6 md:self-start">
        <ActionCtas booking={booking} />
      </aside>
    </main>
  );
}

/** Refund details (task 65b, SRS 11.13.2 "refund details if any"). */
function RefundInfoCard({ booking }: { booking: BookingDetail }) {
  // The schema doesn't record a distinct refund amount or refund mode
  // (SRS 11.14.2 fields don't exist anywhere in Booking/BookingContracts
  // yet) - the only figure available is the booking's own total, shown as
  // the amount under refund rather than inventing a partial-refund figure
  // the backend has no way to compute yet.
  return (
    <Card title="Refund">
      <dl className="flex flex-col gap-2 text-sm">
        <div className="flex items-center justify-between">
          <dt className="text-neutral-600 dark:text-neutral-400">Status</dt>
          <dd className="font-medium">{booking.statusLabel}</dd>
        </div>
        <div className="flex items-center justify-between">
          <dt className="text-neutral-600 dark:text-neutral-400">Amount</dt>
          <dd className="font-medium">₹{booking.price.totalPayable.toFixed(2)}</dd>
        </div>
      </dl>
    </Card>
  );
}

function Timeline({ entries }: { entries: BookingStatusTimelineEntry[] }) {
  if (entries.length === 0) {
    return <p className="text-sm text-neutral-500">No status history yet.</p>;
  }

  return (
    <ol className="flex flex-col gap-4">
      {entries.map((entry, i) => (
        <li key={`${entry.toStatus}-${entry.changedAtUtc}-${i}`} className="flex gap-3">
          <div className="flex flex-col items-center">
            <span className="mt-1 h-2.5 w-2.5 rounded-full bg-black dark:bg-white" aria-hidden="true" />
            {i < entries.length - 1 ? <span className="w-px flex-1 bg-black/15 dark:bg-white/20" /> : null}
          </div>
          <div className="pb-2 text-sm">
            <p className="font-medium">{entry.toStatusLabel}</p>
            <p className="text-neutral-500">{new Date(entry.changedAtUtc).toLocaleString()}</p>
            {entry.reason ? <p className="mt-1 text-neutral-600 dark:text-neutral-400">{entry.reason}</p> : null}
          </div>
        </li>
      ))}
    </ol>
  );
}

/**
 * Action CTAs (task 65c, SRS 11.13.2). Deliberately does not include a
 * cancel/reschedule button: there is no cancellation or reschedule API yet
 * (SRS 11.14/11.15 are unimplemented - no endpoint exists anywhere in
 * BookingsController), and wiring a button to a non-existent endpoint would
 * fail every time it was pressed. Only CTAs backed by a real route/endpoint
 * are shown.
 */
function ActionCtas({ booking }: { booking: BookingDetail }) {
  const canRebook = !!booking.service.slug;

  return (
    <div className="flex flex-col gap-3 rounded-xl border border-black/10 bg-white p-5 dark:border-white/15 dark:bg-neutral-900">
      {canRebook ? (
        <Link href={`/services/${booking.service.slug}`}>
          <Button type="button" className="w-full">
            Book again
          </Button>
        </Link>
      ) : null}
      <Link href="/bookings">
        <Button type="button" variant="secondary" className="w-full">
          Back to my bookings
        </Button>
      </Link>
      {/* No in-app support ticket system exists yet (task 84, Support module
          is unimplemented) - this is a plain mailto link, not a ticketing flow. */}
      <a href={`mailto:support@nestly.app?subject=${encodeURIComponent(`Booking ${booking.id}`)}`}>
        <Button type="button" variant="secondary" className="w-full">
          Contact support
        </Button>
      </a>
    </div>
  );
}

// Mirrors PriceCalculator.tsx's PriceSummary intentionally rather than
// importing it - see booking/summary/page.tsx's PriceRows for the same note.
function PriceRows({ breakdown }: { breakdown: PriceBreakdown }) {
  return (
    <dl className="flex flex-col gap-1.5 text-sm">
      <Row label={`Base price × ${breakdown.quantity}`} value={breakdown.baseTotal} />
      {breakdown.addOnLineItems.map((item) => (
        <Row key={item.addOnId} label={`${item.name} × ${item.quantity}`} value={item.lineTotal} />
      ))}
      {breakdown.visitCharge > 0 ? <Row label="Visit charge" value={breakdown.visitCharge} /> : null}
      <Row label={`Tax (${breakdown.taxPercentage}%)`} value={breakdown.taxAmount} />
      {breakdown.platformFee > 0 ? <Row label="Platform fee" value={breakdown.platformFee} /> : null}
      <div className="mt-1 flex items-center justify-between border-t border-black/10 pt-2 font-semibold dark:border-white/15">
        <dt>Total payable</dt>
        <dd>₹{breakdown.totalPayable.toFixed(2)}</dd>
      </div>
    </dl>
  );
}

function Row({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex items-center justify-between text-neutral-600 dark:text-neutral-400">
      <dt>{label}</dt>
      <dd>₹{value.toFixed(2)}</dd>
    </div>
  );
}
