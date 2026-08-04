"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import {
  BookingStatusBadge,
  formatCalendarDate,
  inr,
} from "@/components/patterns";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, EmptyState, PageHeading, Skeleton, Tabs } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { BookingListItem, BookingStatusBucket } from "@/lib/types";

const TABS: readonly { value: BookingStatusBucket; label: string }[] = [
  { value: "Upcoming", label: "Upcoming" },
  { value: "Completed", label: "Completed" },
  { value: "Cancelled", label: "Cancelled" },
];

const EMPTY_COPY: Record<BookingStatusBucket, { title: string; description: string }> = {
  Upcoming: {
    title: "Nothing booked yet",
    description: "Your next service will show up here the moment it's confirmed.",
  },
  Completed: {
    title: "No completed bookings",
    description: "Once a professional finishes a job, it moves here so you can review it.",
  },
  Cancelled: {
    title: "No cancelled bookings",
    description: "Cancellations and their refunds are kept here for your records.",
  },
};

/**
 * Order history (SRS 11.13.1, task 65a): bookings grouped into the same
 * Upcoming/Completed/Cancelled buckets the list API filters by
 * (BookingStatusMapper.BucketFor) - Payment pending/failed bookings fall
 * under Upcoming there (still actionable, not yet resolved either way).
 *
 * The list API's row shape (BookingListItemResponse) does not carry an
 * address summary, only service/date/amount/status - SRS 11.13.1 asks for
 * one, but adding it isn't a frontend task, it's a backend contract change,
 * so this deliberately doesn't fabricate one; the full address is one tap
 * away on the detail page.
 */
export default function BookingsPage() {
  return (
    <RequireAuth>
      <BookingsScreen />
    </RequireAuth>
  );
}

function BookingsScreen() {
  const [bucket, setBucket] = useState<BookingStatusBucket>("Upcoming");

  const query = useQuery({
    queryKey: ["bookings", bucket],
    queryFn: () =>
      apiFetch<BookingListItem[]>(`${API_V1}/bookings?bucket=${bucket}`, { authenticated: true }),
  });

  return (
    <main className="mx-auto w-full max-w-3xl px-4 py-8 sm:px-6 sm:py-12">
      <PageHeading title="My bookings" subtitle="Track and manage your service bookings." />

      {/* `label` becomes the tablist's accessible name ("Booking status"),
          which is how the E2E suite addresses this group. */}
      <Tabs
        tabs={TABS}
        value={bucket}
        onChange={setBucket}
        label="Booking status"
        className="mb-6"
      />

      {query.isPending ? (
        <ul className="flex flex-col gap-3" aria-hidden>
          {[0, 1, 2].map((row) => (
            <li key={row}>
              <Skeleton className="h-[5.5rem] rounded-2xl" />
            </li>
          ))}
        </ul>
      ) : query.isError ? (
        <Alert
          tone="error"
          title="Couldn't load your bookings"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      ) : query.data.length === 0 ? (
        <EmptyState
          title={EMPTY_COPY[bucket].title}
          description={EMPTY_COPY[bucket].description}
          action={
            <Link
              href="/categories"
              className="inline-flex h-10 items-center justify-center rounded-lg bg-brand-600 px-4 text-sm font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700"
            >
              Browse services
            </Link>
          }
        />
      ) : (
        <ul className="flex animate-fade-in flex-col gap-3">
          {query.data.map((booking) => (
            <li key={booking.id}>
              <Link
                href={`/bookings/${booking.id}`}
                className="group flex items-center justify-between gap-4 rounded-2xl border border-line bg-surface p-4 shadow-sm transition duration-fast ease-out hover:border-line-strong hover:bg-surface-2 hover:shadow-md sm:p-5"
              >
                <span className="flex min-w-0 flex-col gap-1.5">
                  <span className="truncate font-medium text-fg">{booking.serviceName}</span>
                  <span className="nums text-sm text-fg-muted">
                    {formatCalendarDate(booking.slotDate)}
                  </span>
                  <BookingStatusBadge status={booking.status} label={booking.statusLabel} />
                </span>
                <span className="flex shrink-0 items-center gap-3">
                  <span className="nums text-base font-semibold text-fg">
                    {inr(booking.totalPayable)}
                  </span>
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    className="h-4 w-4 text-fg-subtle transition-transform duration-fast ease-out group-hover:translate-x-0.5"
                    aria-hidden
                  >
                    <path d="m9 18 6-6-6-6" />
                  </svg>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
