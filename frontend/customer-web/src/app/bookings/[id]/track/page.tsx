"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { Timeline } from "@/components/patterns";
import { RequireAuth } from "@/components/RequireAuth";
import { TrackingMap } from "@/components/TrackingMap";
import { Alert, Button, Card, LinkButton, PageHeading, Skeleton, cx } from "@/components/ui";
import { isBookingTrackable, useLiveBookingTracking } from "@/hooks/useBookingTracking";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { BookingStatus } from "@/lib/types";
import type { BookingDetail, BookingTrackingResponse } from "@/lib/types";

const TRACKING_POLL_INTERVAL_MS = 15_000;

/** A fix this old is more likely stale device state than the professional's real position - matches ProviderLocationIngestOptions.MaximumAgeMinutes' default on the backend. */
const STALE_FIX_MINUTES = 5;

const STEPS: { status: BookingStatus; label: string }[] = [
  { status: BookingStatus.Assigned, label: "Assigned" },
  { status: BookingStatus.ProviderEnRoute, label: "On the way" },
  { status: BookingStatus.ProviderArrived, label: "Arrived" },
  { status: BookingStatus.InProgress, label: "In progress" },
];

/**
 * The live tracking screen (task 281): map, status stepper, ETA and the
 * provider's card, built on top of the tracking read model (task 275) and
 * kept live by {@link useLiveBookingTracking} (task 273's hub). Reuses the
 * booking detail's own query cache for the timeline/status rather than
 * fetching the booking twice - this screen and the detail page agree on one
 * `["booking", id]` cache.
 */
export default function BookingTrackScreen() {
  return (
    <RequireAuth>
      <TrackScreen />
    </RequireAuth>
  );
}

function TrackScreen() {
  const { id } = useParams<{ id: string }>();

  const bookingQuery = useQuery({
    queryKey: ["booking", id],
    queryFn: () => apiFetch<BookingDetail>(`${API_V1}/bookings/${id}`, { authenticated: true }),
    refetchInterval: (query) =>
      query.state.data && isBookingTrackable(query.state.data.status) ? TRACKING_POLL_INTERVAL_MS : false,
  });

  const trackable = !!bookingQuery.data && isBookingTrackable(bookingQuery.data.status);

  const trackingQuery = useQuery({
    queryKey: ["booking-tracking", id],
    queryFn: () => apiFetch<BookingTrackingResponse>(`${API_V1}/bookings/${id}/tracking`, { authenticated: true }),
    enabled: trackable,
    refetchInterval: trackable ? TRACKING_POLL_INTERVAL_MS : false,
  });

  useLiveBookingTracking(trackable ? id : undefined);

  if (bookingQuery.isPending) {
    return <TrackingScreenSkeleton />;
  }

  if (bookingQuery.isError || !bookingQuery.data) {
    return (
      <main className="mx-auto w-full max-w-2xl px-4 py-12 sm:px-6">
        <Alert
          tone="error"
          title="Couldn't load this booking"
          action={
            <Button size="sm" variant="secondary" onClick={() => bookingQuery.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(bookingQuery.error)}
        </Alert>
      </main>
    );
  }

  const booking = bookingQuery.data;

  if (!trackable) {
    return (
      <main className="mx-auto w-full max-w-2xl px-4 py-12 sm:px-6">
        <PageHeading title="Not trackable right now" subtitle={booking.service.name} />
        <div className="mt-6">
          <Alert tone="info" title={booking.statusLabel}>
            {booking.status < BookingStatus.Assigned
              ? "Live tracking starts once a professional is assigned to this booking."
              : "This booking is no longer trackable."}
          </Alert>
        </div>
        <LinkButton href={`/bookings/${booking.id}`} variant="secondary" className="mt-6">
          Back to booking
        </LinkButton>
      </main>
    );
  }

  const tracking = trackingQuery.data;

  return (
    <main className="mx-auto flex w-full max-w-2xl flex-col gap-6 px-4 py-8 sm:px-6 sm:py-10">
      <PageHeading title="Tracking your booking" subtitle={booking.service.name} />

      <TrackingMap
        providerLocation={
          tracking?.providerLocation
            ? { latitude: tracking.providerLocation.latitude, longitude: tracking.providerLocation.longitude }
            : null
        }
        destination={{ latitude: booking.address.latitude, longitude: booking.address.longitude }}
      />

      <Card>
        <StatusStepper currentStatus={booking.status} />
      </Card>

      {trackingQuery.isPending ? (
        <Card title="Estimated arrival">
          <Skeleton className="h-4 w-40" />
        </Card>
      ) : (
        <EtaCard eta={tracking?.eta ?? null} location={tracking?.providerLocation ?? null} />
      )}

      {tracking?.provider ? <ProviderCard provider={tracking.provider} /> : null}

      <Card title="Status timeline">
        <Timeline
          entries={booking.timeline}
          currentStatus={booking.status}
          providerAssignmentStatus={booking.providerAssignmentStatus}
        />
      </Card>

      <Link
        href={`/bookings/${booking.id}`}
        className="text-center text-sm font-medium text-fg-muted transition duration-fast ease-out hover:text-fg"
      >
        Back to booking details
      </Link>
    </main>
  );
}

function StatusStepper({ currentStatus }: { currentStatus: BookingStatus }) {
  const currentIndex = STEPS.findIndex((step) => step.status === currentStatus);

  return (
    <ol className="flex items-center justify-between">
      {STEPS.map((step, index) => {
        const reached = currentIndex >= index;
        const isCurrent = currentIndex === index;
        return (
          <li key={step.status} className="flex flex-1 flex-col items-center gap-1.5 last:flex-none">
            <div className="flex w-full items-center">
              <span
                aria-hidden
                className={cx(
                  "flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold",
                  reached ? "bg-brand-600 text-fg-on-brand" : "bg-surface-3 text-fg-subtle",
                  isCurrent && "animate-pop ring-4 ring-brand-600/20",
                )}
              >
                {index + 1}
              </span>
              {index < STEPS.length - 1 ? (
                <span
                  aria-hidden
                  className={cx("mx-1 h-px flex-1", currentIndex > index ? "bg-brand-600" : "bg-line")}
                />
              ) : null}
            </div>
            <span className={cx("text-xs font-medium", reached ? "text-fg" : "text-fg-subtle")}>
              {step.label}
              {isCurrent ? <span className="sr-only"> (current)</span> : null}
            </span>
          </li>
        );
      })}
    </ol>
  );
}

function EtaCard({
  eta,
  location,
}: {
  eta: { etaSeconds: number; etaComputedAtUtc: string } | null;
  location: { recordedAtUtc: string } | null;
}) {
  const stale = location ? minutesSince(location.recordedAtUtc) >= STALE_FIX_MINUTES : false;

  return (
    <Card title="Estimated arrival">
      {eta ? (
        <div className="flex items-center justify-between gap-3">
          <p className="text-lg font-semibold text-fg">Arriving in ~{formatMinutes(eta.etaSeconds)}</p>
          {stale ? (
            <span className="inline-flex items-center gap-1.5 rounded-full bg-warning-soft px-2.5 py-1 text-xs font-medium text-warning">
              <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-warning" />
              Last update a few minutes ago
            </span>
          ) : null}
        </div>
      ) : (
        <p className="text-sm text-fg-muted">Calculating the estimated arrival time…</p>
      )}
    </Card>
  );
}

function ProviderCard({
  provider,
}: {
  provider: { displayName: string; photoUrl: string | null; rating: number | null; maskedPhone: string | null };
}) {
  return (
    <Card>
      <div className="flex items-center gap-4">
        {provider.photoUrl ? (
          // eslint-disable-next-line @next/next/no-img-element -- a remote provider photo, not an app asset next/image can optimise usefully here
          <img
            src={provider.photoUrl}
            alt=""
            className="h-12 w-12 shrink-0 rounded-full object-cover ring-2 ring-line"
          />
        ) : (
          <span
            aria-hidden
            className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-surface-3 text-sm font-semibold text-fg-subtle ring-2 ring-line"
          >
            {provider.displayName.charAt(0).toUpperCase()}
          </span>
        )}
        <div className="min-w-0 flex-1">
          <p className="truncate font-medium text-fg">{provider.displayName}</p>
          {provider.rating !== null ? (
            <p className="mt-0.5 flex items-center gap-1 text-sm text-fg-muted">
              <svg viewBox="0 0 24 24" fill="currentColor" className="h-3.5 w-3.5 text-accent-500" aria-hidden>
                <path d="M12 2.5 14.9 8.6l6.6.9-4.8 4.7 1.2 6.6-6-3.1-6 3.1 1.2-6.6-4.8-4.7 6.6-.9L12 2.5Z" />
              </svg>
              <span className="nums">{provider.rating.toFixed(1)}</span>
            </p>
          ) : null}
        </div>
        {provider.maskedPhone ? (
          // Not a `tel:` link: the number is masked at the API boundary and
          // never leaves the server whole (ContactMasking.MaskOrNull on the
          // backend) - this exists so a customer recognises the number
          // calling them, not so this button can dial it. See
          // TrackedProviderSummary's doc comment for the reasoning.
          <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-line bg-surface-2 px-3 py-1.5 text-xs font-medium text-fg-muted">
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="h-3.5 w-3.5"
              aria-hidden
            >
              <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 22 16.92Z" />
            </svg>
            <span className="nums">{provider.maskedPhone}</span>
          </span>
        ) : null}
      </div>
    </Card>
  );
}

function minutesSince(utc: string): number {
  const then = new Date(utc).getTime();
  if (Number.isNaN(then)) return 0;
  return (Date.now() - then) / 60_000;
}

function formatMinutes(seconds: number): string {
  const minutes = Math.max(1, Math.round(seconds / 60));
  return `${minutes} min`;
}

/**
 * Bespoke (map + stepper + provider card), not the shared `ScreenSkeleton`
 * from `patterns.tsx` (that one is a generic card stack and doesn't fit this
 * layout) — named distinctly so it doesn't shadow the shared component of
 * the same name for anyone reading or importing from this file.
 */
function TrackingScreenSkeleton() {
  return (
    <main className="mx-auto flex w-full max-w-2xl flex-col gap-6 px-4 py-8 sm:px-6 sm:py-10" aria-hidden>
      <Skeleton className="h-8 w-56" />
      <Skeleton className="h-48 rounded-2xl" />
      <Skeleton className="h-20 rounded-2xl" />
      <Skeleton className="h-16 rounded-2xl" />
      <Skeleton className="h-24 rounded-2xl" />
    </main>
  );
}
