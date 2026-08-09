"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import type { ReactNode } from "react";
import { ChatWidget } from "@/components/ChatWidget";
import {
  BookingStatusBadge,
  DetailList,
  DetailRow,
  PriceBreakdownList,
  ScreenSkeleton,
  Timeline,
  formatCalendarDate,
  formatInstant,
  formatTimeRange,
  inr,
  providerAssignmentLabel,
  providerAssignmentTone,
} from "@/components/patterns";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Badge, Button, Card, PageHeading, Skeleton, cx } from "@/components/ui";
import { isBookingTrackable, useBookingTracking } from "@/hooks/useBookingTracking";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import {
  BookingProviderAssignmentStatus,
  BookingStatus,
  ChatContextType,
  RefundMethod,
  RefundStatus,
  RefundType,
} from "@/lib/types";
import type {
  BookingCompletionProofResponse,
  BookingDetail,
  RefundTransactionResponse,
} from "@/lib/types";

/** REST poll cadence while a booking is trackable - the socket (useBookingTracking) is the fast path, this is the guaranteed one. */
const TRACKING_POLL_INTERVAL_MS = 15_000;

/** Both reachable only via RefundPending/Refunded in BookingLifecycle - see BookingStatusMapper.cs. */
const REFUND_STATUSES: BookingStatus[] = [BookingStatus.RefundPending, BookingStatus.Refunded];

/** Cancelling is pointless once the booking is already in a terminal state. */
const CLOSED_STATUSES: BookingStatus[] = [
  BookingStatus.Completed,
  BookingStatus.CancelledByCustomer,
  BookingStatus.CancelledByAdmin,
  BookingStatus.RefundPending,
  BookingStatus.Refunded,
];

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
    // Polling fallback for whenever the socket below never connects - stops
    // the instant the booking leaves a trackable status, so a closed booking
    // is never re-fetched on a timer. The socket, when it does connect, wins
    // the race in practice since a status change invalidates immediately.
    refetchInterval: (query) =>
      query.state.data && isBookingTrackable(query.state.data.status) ? TRACKING_POLL_INTERVAL_MS : false,
  });

  useBookingTracking(query.data && isBookingTrackable(query.data.status) ? id : undefined);

  if (query.isPending) {
    return <ScreenSkeleton cards={4} className="mx-auto w-full max-w-4xl px-4 py-8 sm:px-6 sm:py-10" />;
  }

  if (query.isError || !query.data) {
    return (
      <main className="mx-auto w-full max-w-3xl px-4 py-12 sm:px-6">
        <Alert
          tone="error"
          title="Couldn't load this booking"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      </main>
    );
  }

  const booking = query.data;
  const isClosed = CLOSED_STATUSES.includes(booking.status);

  return (
    <main className="mx-auto grid w-full max-w-4xl gap-6 px-4 py-8 sm:px-6 sm:py-10 md:grid-cols-[1fr_20rem]">
      <div className="flex min-w-0 flex-col gap-6">
        {/* The heading is the service name and the subtitle carries the
            booking id as one text node - both are addressed by name in the
            E2E suite, so neither shape changes. */}
        <PageHeading
          title={booking.service.name}
          subtitle={`Booking ID: ${booking.id}`}
          actions={<BookingStatusBadge status={booking.status} label={booking.statusLabel} />}
        />

        <StatusSummaryCard booking={booking} />

        <Card title="Slot">
          <DetailList>
            <DetailRow label="Date">{formatCalendarDate(booking.slot.date)}</DetailRow>
            <DetailRow label="Time window" numeric>
              {booking.slot.name} · {formatTimeRange(booking.slot.startTime, booking.slot.endTime)}
            </DetailRow>
            <DetailRow label="Booked on">{formatInstant(booking.createdAtUtc)}</DetailRow>
          </DetailList>
        </Card>

        <Card title="Address">
          <address className="text-sm not-italic leading-relaxed text-fg-muted">
            <span className="font-medium text-fg">{booking.address.label}</span>
            <br />
            {booking.address.line1}
            {booking.address.line2 ? <>, {booking.address.line2}</> : null}
            {booking.address.landmark ? <>, near {booking.address.landmark}</> : null}
            <br />
            {booking.address.city}, {booking.address.state}{" "}
            <span className="nums">{booking.address.pincode}</span>
            <br />
            {booking.address.contactName} · <span className="nums">{booking.address.contactMobile}</span>
          </address>
        </Card>

        <Card title="Price & payment">
          <PriceBreakdownList
            breakdown={booking.price}
            discount={
              booking.couponDiscountAmount
                ? { code: booking.couponCode, amount: booking.couponDiscountAmount }
                : null
            }
            walletCreditApplied={booking.walletCreditApplied}
            total={booking.finalPayable}
            totalLabel="Amount paid"
          />
        </Card>

        {REFUND_STATUSES.includes(booking.status) ? <RefundInfoCard booking={booking} /> : null}

        {booking.status === BookingStatus.Completed ? (
          <CompletionProofCard bookingId={booking.id} />
        ) : null}

        <Card title="Status timeline">
          <Timeline
            entries={booking.timeline}
            currentStatus={booking.status}
            providerAssignmentStatus={booking.providerAssignmentStatus}
          />
        </Card>

        <ChatWidget contextType={ChatContextType.Booking} contextId={booking.id} />
      </div>

      <aside className="flex flex-col gap-4 md:sticky md:top-20 md:self-start">
        <ActionCtas booking={booking} isClosed={isClosed} />
      </aside>
    </main>
  );
}

/**
 * The "where is my booking right now" card. Surfaces
 * `providerAssignmentStatus` (task 208) — the booking's own status stays
 * "Assigned" across both the offer to a professional and their acceptance, so
 * without this the customer has no way to tell an accepted job from one still
 * waiting on a response.
 */
function StatusSummaryCard({ booking }: { booking: BookingDetail }) {
  const assignment = booking.providerAssignmentStatus;

  return (
    <Card>
      <div className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="min-w-0">
            <p className="text-xs font-medium uppercase tracking-wide text-fg-subtle">
              Current status
            </p>
            <p className="mt-1 text-lg font-semibold text-fg">{booking.statusLabel}</p>
          </div>
          <BookingStatusBadge status={booking.status} label={booking.statusLabel} />
        </div>

        <div className="flex items-start gap-3 rounded-xl border border-line bg-surface-2 px-4 py-3">
          {booking.provider?.photoUrl ? (
            // eslint-disable-next-line @next/next/no-img-element -- a remote provider photo, not an app asset next/image can optimise usefully here
            <img
              src={booking.provider.photoUrl}
              alt=""
              className="mt-0.5 h-8 w-8 shrink-0 rounded-full object-cover ring-1 ring-line"
            />
          ) : (
          <span
            aria-hidden
            className={cx(
              "mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full",
              assignment === BookingProviderAssignmentStatus.Accepted
                ? "bg-success-soft text-success"
                : assignment === null
                  ? "bg-surface-3 text-fg-subtle"
                  : "bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-300",
            )}
          >
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="h-4 w-4"
            >
              <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
              <circle cx="9" cy="7" r="4" />
            </svg>
          </span>
          )}
          <div className="min-w-0">
            <p className="text-sm font-medium text-fg">
              {booking.provider ? booking.provider.displayName : "Your professional"}
            </p>
            <p className="mt-0.5 text-sm leading-relaxed text-fg-muted">
              {assignment === null
                ? "Not assigned yet — we'll match a professional to this slot shortly."
                : providerAssignmentLabel(assignment)}
            </p>
            {/* Task 293: real values at last. Both stay optional and both go
                missing for ordinary reasons - no approved photo yet, no
                visible reviews yet - so a null renders as the placeholder
                avatar and no stars rather than a zero. */}
            {booking.provider && booking.provider.rating !== null ? (
              <p className="mt-1 flex items-center gap-1 text-sm text-fg-muted">
                <svg viewBox="0 0 24 24" fill="currentColor" className="h-3.5 w-3.5 text-accent-500" aria-hidden>
                  <path d="M12 2.5 14.9 8.6l6.6.9-4.8 4.7 1.2 6.6-6-3.1-6 3.1 1.2-6.6-4.8-4.7 6.6-.9L12 2.5Z" />
                </svg>
                <span className="nums">{booking.provider.rating.toFixed(1)}</span>
              </p>
            ) : null}
          </div>
          {assignment !== null ? (
            <Badge tone={providerAssignmentTone(assignment)} className="ml-auto shrink-0">
              {assignment === BookingProviderAssignmentStatus.Accepted ? "Confirmed" : "In progress"}
            </Badge>
          ) : null}
        </div>

        {isBookingTrackable(booking.status) ? (
          <Link
            href={`/bookings/${booking.id}/track`}
            className="inline-flex h-11 items-center justify-center gap-2 rounded-lg bg-brand-600 px-4 text-sm font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700 active:scale-[0.98]"
          >
            <span aria-hidden className="h-2 w-2 animate-pulse rounded-full bg-fg-on-brand" />
            Track live
          </Link>
        ) : null}
      </div>
    </Card>
  );
}

/**
 * Refund details (task 65b, 78c, SRS 11.13.2 "refund details if any"). Now
 * backed by the real refund transaction(s) (GET /bookings/{id}/refunds)
 * instead of fabricating an amount from the booking's total.
 */
function RefundInfoCard({ booking }: { booking: BookingDetail }) {
  const refundsQuery = useQuery({
    queryKey: ["booking-refunds", booking.id],
    queryFn: () =>
      apiFetch<RefundTransactionResponse[]>(`${API_V1}/bookings/${booking.id}/refunds`, {
        authenticated: true,
      }),
  });

  if (refundsQuery.isPending) {
    return (
      <Card title="Refund">
        <div className="flex flex-col gap-2.5" aria-hidden>
          <Skeleton className="h-3.5 w-full" />
          <Skeleton className="h-3.5 w-2/3" />
        </div>
      </Card>
    );
  }

  if (refundsQuery.isError) {
    return (
      <Card title="Refund">
        <Alert
          tone="error"
          title="Couldn't load refund details"
          action={
            <Button size="sm" variant="secondary" onClick={() => refundsQuery.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(refundsQuery.error)}
        </Alert>
      </Card>
    );
  }

  const refunds = refundsQuery.data;

  if (refunds.length === 0) {
    // The booking reached a refund-related status but no refund transaction
    // has been recorded yet - a reasonable "still processing" fallback
    // rather than rendering nothing.
    return (
      <Card title="Refund">
        <Alert tone="info" title={booking.statusLabel}>
          Your refund is being processed. We&apos;ll update this page once it&apos;s initiated.
        </Alert>
      </Card>
    );
  }

  return (
    <Card title="Refund">
      <div className="flex flex-col gap-5">
        {refunds.map((refund) => (
          <div key={refund.id} className="border-t border-line pt-4 first:border-t-0 first:pt-0">
            <div className="mb-3 flex items-center justify-between gap-3">
              <span className="nums text-xl font-semibold text-fg">{inr(refund.amount)}</span>
              <Badge tone={refundStatusTone(refund.status)}>{refundStatusLabel(refund.status)}</Badge>
            </div>
            <DetailList>
              <DetailRow label="Refund amount" numeric>
                {inr(refund.amount)}
              </DetailRow>
              <DetailRow label="Type">{refundTypeLabel(refund.type)}</DetailRow>
              <DetailRow label="Method">{refundMethodLabel(refund.method)}</DetailRow>
              <DetailRow label="Initiated">{formatInstant(refund.createdAtUtc)}</DetailRow>
              {refund.gatewayRefundRef ? (
                <DetailRow label="Reference" numeric>
                  <span className="break-all">{refund.gatewayRefundRef}</span>
                </DetailRow>
              ) : null}
            </DetailList>
          </div>
        ))}
      </div>
    </Card>
  );
}

function refundTypeLabel(type: RefundType): string {
  return type === RefundType.Full ? "Full refund" : "Partial refund";
}

function refundMethodLabel(method: RefundMethod): string {
  return method === RefundMethod.Gateway ? "Original payment method" : "Wallet credit";
}

function refundStatusLabel(status: RefundStatus): string {
  switch (status) {
    case RefundStatus.Initiated:
      return "Initiated";
    case RefundStatus.Processing:
      return "Processing";
    case RefundStatus.Refunded:
      return "Refunded";
    case RefundStatus.Failed:
      return "Failed";
    default:
      return "Unknown";
  }
}

function refundStatusTone(status: RefundStatus) {
  switch (status) {
    case RefundStatus.Refunded:
      return "success" as const;
    case RefundStatus.Failed:
      return "danger" as const;
    default:
      return "info" as const;
  }
}

/**
 * Action CTAs (task 65c, SRS 11.13.2), wired to the real post-booking flows
 * (tasks 89a-c, 90, 91, 92). Every action is a styled `Link` rather than
 * `<Link><Button/></Link>` — a button inside an anchor is invalid HTML and
 * exposes two nested controls for one action. The accessible names
 * "Cancel booking" and "Reschedule booking" are addressed by the E2E suite
 * and must stay exactly as written.
 *
 * Cancel is separated below a rule and carries the danger tone: it is the one
 * irreversible action here, and grouping it with "Book again" invites the
 * mis-click. Each target page still gates on its own eligibility endpoint and
 * explains why an action isn't available.
 */
function ActionCtas({ booking, isClosed }: { booking: BookingDetail; isClosed: boolean }) {
  const canRebook = !!booking.service.slug;

  return (
    <div className="flex flex-col gap-2.5 rounded-2xl border border-line bg-surface p-4 shadow-sm">
      {canRebook ? (
        <ActionLink href={`/services/${booking.service.slug}`} variant="primary">
          Book again
        </ActionLink>
      ) : null}

      {!isClosed ? (
        <ActionLink href={`/bookings/${booking.id}/reschedule`}>Reschedule booking</ActionLink>
      ) : null}

      {canRebook ? (
        <ActionLink href={`/recurring-bookings/new?serviceSlug=${booking.service.slug}`}>
          Set up recurring booking
        </ActionLink>
      ) : null}

      <ActionLink href={`/bookings/${booking.id}/review`}>Leave a review</ActionLink>

      {booking.status === BookingStatus.Completed ? (
        <ActionLink href="/refer-earn">Refer a friend &amp; earn</ActionLink>
      ) : null}

      <ActionLink href={`/support/new?bookingId=${booking.id}`}>Raise an issue</ActionLink>

      <ActionLink href="/bookings">Back to my bookings</ActionLink>

      {/* Kept as an unconditional fallback alongside the real ticketing flow
          above - useful if the in-app flow itself is the thing that's broken. */}
      <a
        href={`mailto:support@nestly.app?subject=${encodeURIComponent(`Booking ${booking.id}`)}`}
        className="inline-flex h-10 w-full items-center justify-center rounded-lg text-sm font-medium text-fg-muted transition duration-fast ease-out hover:bg-surface-3 hover:text-fg"
      >
        Email support
      </a>

      {!isClosed ? (
        <div className="mt-1 border-t border-line pt-3">
          <ActionLink href={`/bookings/${booking.id}/cancel`} variant="danger">
            Cancel booking
          </ActionLink>
          <p className="mt-2 text-xs leading-relaxed text-fg-subtle">
            Cancelling can&apos;t be undone. You&apos;ll see the exact refund before you confirm.
          </p>
        </div>
      ) : null}
    </div>
  );
}

function ActionLink({
  href,
  children,
  variant = "secondary",
}: {
  href: string;
  children: ReactNode;
  variant?: "primary" | "secondary" | "danger";
}) {
  return (
    <Link
      href={href}
      className={cx(
        "inline-flex h-10 w-full items-center justify-center rounded-lg px-4 text-sm font-medium transition duration-fast ease-out active:scale-[0.98]",
        variant === "primary" &&
          "bg-brand-600 text-fg-on-brand shadow-brand hover:bg-brand-700",
        variant === "secondary" &&
          "border border-line bg-surface text-fg shadow-xs hover:border-line-strong hover:bg-surface-2",
        variant === "danger" &&
          "border border-danger/30 bg-danger-soft text-danger hover:border-danger/50",
      )}
    >
      {children}
    </Link>
  );
}

/** Photo + checklist evidence the provider submitted at job completion (tasks 195-198). */
function CompletionProofCard({ bookingId }: { bookingId: string }) {
  const query = useQuery({
    queryKey: ["booking-completion-proof", bookingId],
    queryFn: async () => {
      const proof = await apiFetch<BookingCompletionProofResponse | undefined>(
        `${API_V1}/bookings/${bookingId}/completion-proof`,
        { authenticated: true },
      );
      /**
       * The endpoint answers 204 until the provider has actually submitted
       * proof, and `apiFetch` maps that to `undefined`. React Query treats an
       * `undefined` resolution as a thrown failure, so this card used to show
       * "Couldn't load the completion proof" for every booking that simply
       * did not have any yet — which is most of them. Normalising to `null`
       * makes "none submitted" a value rather than an error.
       */
      return proof ?? null;
    },
  });

  if (query.isPending) {
    return (
      <Card title="Completion proof">
        <div className="flex flex-col gap-2.5" aria-hidden>
          <Skeleton className="h-3.5 w-2/3" />
          <Skeleton className="h-3.5 w-1/2" />
        </div>
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Completion proof">
        <Alert
          tone="error"
          title="Couldn't load the completion proof"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      </Card>
    );
  }

  const proof = query.data;
  if (!proof) {
    return null;
  }

  return (
    <Card title="Completion proof">
      <p className="text-sm text-fg-muted">
        Submitted {formatInstant(proof.submittedAtUtc)} ·{" "}
        <span className="nums">{proof.photoRefs.length}</span> photo
        {proof.photoRefs.length === 1 ? "" : "s"}
      </p>
      {proof.checklistAnswers.length > 0 ? (
        <ul className="mt-3 flex flex-col gap-2">
          {proof.checklistAnswers.map((answer, index) => (
            <li key={index} className="flex items-start gap-2 text-sm text-fg">
              <span
                aria-hidden
                className={cx(
                  "mt-0.5 flex h-4 w-4 shrink-0 items-center justify-center rounded-full",
                  answer.completed ? "bg-success-soft text-success" : "bg-surface-3 text-fg-subtle",
                )}
              >
                {answer.completed ? (
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="3"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    className="h-2.5 w-2.5"
                  >
                    <path d="m5 13 4 4L19 7" />
                  </svg>
                ) : null}
              </span>
              <span className="min-w-0">
                <span className="sr-only">{answer.completed ? "Done: " : "Not done: "}</span>
                {answer.item}
                {answer.notes ? (
                  <span className="block text-fg-muted">{answer.notes}</span>
                ) : null}
              </span>
            </li>
          ))}
        </ul>
      ) : null}
    </Card>
  );
}
