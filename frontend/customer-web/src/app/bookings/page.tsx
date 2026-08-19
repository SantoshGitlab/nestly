"use client";

import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import {
  BannerBreadcrumb,
  BookingStatusBadge,
  formatCalendarDate,
  inr,
} from "@/components/patterns";
import { MotionLink, Reveal, RevealItem } from "@/components/motion";
import { PageBanner } from "@/components/PageBanner";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, EmptyState, LinkButton, Skeleton, Tabs } from "@/components/ui";
import { isBookingTrackable } from "@/hooks/useBookingTracking";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { BookingStatus } from "@/lib/types";
import type { BookingDetail, BookingListResponse, BookingStatusBucket, ReviewResponse } from "@/lib/types";

const PAGE_SIZE = 20;

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

  const query = useInfiniteQuery({
    queryKey: ["bookings", bucket],
    queryFn: ({ pageParam }) =>
      apiFetch<BookingListResponse>(
        `${API_V1}/bookings?bucket=${bucket}&page=${pageParam}&pageSize=${PAGE_SIZE}`,
        { authenticated: true },
      ),
    initialPageParam: 1,
    getNextPageParam: (lastPage) =>
      lastPage.page * lastPage.pageSize < lastPage.totalCount ? lastPage.page + 1 : undefined,
  });

  const bookings = query.data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <main className="flex w-full flex-col">
      <PageBanner
        title="My bookings"
        description="Track and manage your service bookings."
        breadcrumb={<BannerBreadcrumb items={[{ label: "Home", href: "/" }, { label: "My bookings" }]} />}
      />

      <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14">
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
      <div className="lg:col-span-8">
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
      ) : bookings.length === 0 ? (
        <EmptyState
          title={EMPTY_COPY[bucket].title}
          description={EMPTY_COPY[bucket].description}
          action={<LinkButton href="/categories">Browse services</LinkButton>}
        />
      ) : (
        <Reveal as="ul" className="flex flex-col gap-3">
          {bookings.map((booking) => (
            <RevealItem key={booking.id}>
              {/* relative wrapper, not the row link itself: the Track pill
                  below is a sibling <Link> stacked on top via absolute
                  positioning, not a descendant of the row's own <Link> - a
                  button/link nested inside an anchor is invalid HTML (see
                  ActionCtas' doc comment on the booking detail page for the
                  same rule). */}
              <div className="relative">
                <MotionLink
                  href={`/bookings/${booking.id}`}
                  variant="nudge"
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
                </MotionLink>

                {isBookingTrackable(booking.status) ? (
                  <Link
                    href={`/bookings/${booking.id}/track`}
                    className="absolute right-4 top-4 inline-flex h-7 items-center gap-1 rounded-full bg-brand-600 px-3 text-xs font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700 sm:right-5 sm:top-5"
                  >
                    <span aria-hidden className="h-1.5 w-1.5 animate-pulse rounded-full bg-fg-on-brand" />
                    Track
                  </Link>
                ) : null}
              </div>
            </RevealItem>
          ))}
        </Reveal>
      )}

      {query.hasNextPage ? (
        <div className="mt-6 flex justify-center">
          <Button
            variant="secondary"
            loading={query.isFetchingNextPage}
            onClick={() => query.fetchNextPage()}
          >
            Load more
          </Button>
        </div>
      ) : null}
      </div>

      <div className="flex flex-col gap-6 lg:col-span-4 lg:sticky lg:top-20 lg:self-start">
        <LastBookingCard />

        <Card
          title="AMC plans"
          description="Cover an appliance for a fixed number of visits over the year, redeemed as bookings whenever you need one."
        >
          <LinkButton href="/amc" variant="secondary">
            View AMC plans
          </LinkButton>
        </Card>

        <Card
          title="Need help with a booking?"
          description="Raise an issue on any past or upcoming booking and our support team will help."
        >
          <LinkButton href="/support" variant="secondary">
            Contact support
          </LinkButton>
        </Card>
      </div>
      </div>
      </div>
    </main>
  );
}

/**
 * The most recent booking overall (not scoped to whichever tab is active) -
 * date, completion status, assigned professional and review status at a
 * glance, per the customer's own ask for "relevant" sidebar content rather
 * than the generic account nav this replaced.
 *
 * Three queries, each depending on the last: the list endpoint's row shape
 * carries no provider or review info (BookingListItem, same reasoning as
 * this file's own doc comment on why it doesn't fabricate an address
 * summary), so the provider comes from the detail endpoint and the review
 * from its own - the same two calls the booking detail/review pages already
 * make, not a new backend shape invented for this card.
 */
function LastBookingCard() {
  const latestQuery = useQuery({
    queryKey: ["bookings", "latest"],
    queryFn: () =>
      apiFetch<BookingListResponse>(`${API_V1}/bookings?page=1&pageSize=1`, { authenticated: true }),
  });

  const latest = latestQuery.data?.items[0];

  const detailQuery = useQuery({
    queryKey: ["booking", latest?.id],
    queryFn: () => apiFetch<BookingDetail>(`${API_V1}/bookings/${latest!.id}`, { authenticated: true }),
    enabled: !!latest,
  });

  const reviewQuery = useQuery({
    queryKey: ["booking-review", latest?.id],
    queryFn: () =>
      apiFetch<ReviewResponse | undefined>(`${API_V1}/bookings/${latest!.id}/review`, {
        authenticated: true,
      }),
    enabled: !!latest,
  });

  if (latestQuery.isPending) {
    return (
      <Card title="Your last booking">
        <div className="flex flex-col gap-2.5" aria-hidden>
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-3.5 w-28" />
          <Skeleton className="h-3.5 w-32" />
          <Skeleton className="h-3.5 w-24" />
        </div>
      </Card>
    );
  }

  if (latestQuery.isError) {
    return (
      <Card title="Your last booking">
        <Alert
          tone="error"
          action={
            <Button size="sm" variant="secondary" onClick={() => latestQuery.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(latestQuery.error)}
        </Alert>
      </Card>
    );
  }

  // No card at all once we know there has never been a booking - the main
  // column's own EmptyState already covers "nothing here yet" for whichever
  // tab is open, and a second empty state in the sidebar would just repeat it.
  if (!latest) {
    return null;
  }

  const provider = detailQuery.data?.provider ?? null;
  const review = reviewQuery.data;
  const isCompleted = latest.status === BookingStatus.Completed;

  return (
    <Card title="Your last booking">
      <div className="flex flex-col gap-3">
        <div className="min-w-0">
          <p className="truncate font-medium text-fg">{latest.serviceName}</p>
          <p className="nums mt-0.5 text-sm text-fg-muted">{formatCalendarDate(latest.slotDate)}</p>
        </div>

        <BookingStatusBadge status={latest.status} label={latest.statusLabel} />

        <div className="border-t border-line pt-3">
          <p className="text-xs font-medium uppercase tracking-wide text-fg-subtle">Professional</p>
          {/* detailQuery is still in flight the instant `latest` resolves -
              rendered as "assigning", not "not assigned yet", so this never
              flashes a wrong answer before settling on the real one. */}
          <p className="mt-1 text-sm text-fg">
            {detailQuery.isPending
              ? "Loading…"
              : provider
                ? provider.displayName
                : "Not assigned yet"}
          </p>
          {provider && provider.rating !== null ? (
            <p className="mt-1 flex items-center gap-1 text-sm text-fg-muted">
              <svg viewBox="0 0 24 24" fill="currentColor" className="h-3.5 w-3.5 text-accent-500" aria-hidden>
                <path d="M12 2.5 14.9 8.6l6.6.9-4.8 4.7 1.2 6.6-6-3.1-6 3.1 1.2-6.6-4.8-4.7 6.6-.9L12 2.5Z" />
              </svg>
              <span className="nums">{provider.rating.toFixed(1)}</span>
            </p>
          ) : null}
        </div>

        <div className="border-t border-line pt-3">
          <p className="text-xs font-medium uppercase tracking-wide text-fg-subtle">Your review</p>
          {reviewQuery.isPending ? (
            <p className="mt-1 text-sm text-fg-subtle">Loading…</p>
          ) : review ? (
            <p className="mt-1 flex items-center gap-1.5 text-sm text-fg">
              <svg viewBox="0 0 24 24" fill="currentColor" className="h-3.5 w-3.5 text-accent-500" aria-hidden>
                <path d="M12 2.5 14.9 8.6l6.6.9-4.8 4.7 1.2 6.6-6-3.1-6 3.1 1.2-6.6-4.8-4.7 6.6-.9L12 2.5Z" />
              </svg>
              <span className="nums">{review.rating.toFixed(1)}</span>
              <span className="text-fg-muted">· Submitted</span>
            </p>
          ) : isCompleted ? (
            <Link
              href={`/bookings/${latest.id}/review`}
              className="mt-1 inline-block text-sm font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
            >
              Leave a review
            </Link>
          ) : (
            <p className="mt-1 text-sm text-fg-subtle">Not yet completed</p>
          )}
        </div>

        <LinkButton href={`/bookings/${latest.id}`} variant="secondary" size="sm" className="mt-1 self-start">
          View booking
        </LinkButton>
      </div>
    </Card>
  );
}
