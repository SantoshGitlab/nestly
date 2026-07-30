"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { BookingListItem, BookingStatusBucket } from "@/lib/types";

const TABS: { bucket: BookingStatusBucket; label: string }[] = [
  { bucket: "Upcoming", label: "Upcoming" },
  { bucket: "Completed", label: "Completed" },
  { bucket: "Cancelled", label: "Cancelled" },
];

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
    <main className="mx-auto w-full max-w-3xl px-6 py-12">
      <PageHeading title="My bookings" subtitle="Track and manage your service bookings." />

      <div role="tablist" aria-label="Booking status" className="mb-6 flex gap-2 border-b border-black/10 dark:border-white/15">
        {TABS.map((tab) => (
          <button
            key={tab.bucket}
            type="button"
            role="tab"
            aria-selected={bucket === tab.bucket}
            onClick={() => setBucket(tab.bucket)}
            className={`-mb-px border-b-2 px-3 py-2 text-sm font-medium ${
              bucket === tab.bucket
                ? "border-black text-black dark:border-white dark:text-white"
                : "border-transparent text-neutral-500 hover:text-black dark:hover:text-white"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {query.isPending ? (
        <p className="text-sm text-neutral-500">Loading bookings…</p>
      ) : query.isError ? (
        <Alert>{describeError(query.error)}</Alert>
      ) : query.data.length === 0 ? (
        <Card title="No bookings here yet">
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            Bookings in this category will show up here once you have some.
          </p>
        </Card>
      ) : (
        <ul className="flex flex-col gap-4">
          {query.data.map((booking) => (
            <li key={booking.id}>
              <Link href={`/bookings/${booking.id}`}>
                <Card title={booking.serviceName}>
                  <div className="flex items-center justify-between gap-3 text-sm">
                    <div className="flex flex-col gap-1 text-neutral-600 dark:text-neutral-400">
                      <span>{booking.slotDate}</span>
                      <span>{booking.statusLabel}</span>
                    </div>
                    <span className="font-semibold">₹{booking.totalPayable.toFixed(2)}</span>
                  </div>
                </Card>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
