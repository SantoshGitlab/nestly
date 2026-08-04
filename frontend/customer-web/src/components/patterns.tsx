"use client";

import type { ReactNode } from "react";
import { Badge, Skeleton, cx } from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import {
  BookingPartnerAssignmentStatus,
  BookingStatus,
  RecurringBookingPlanStatus,
  SupportTicketStatus,
} from "@/lib/types";
import type { PriceBreakdown } from "@/lib/types";

/**
 * Screen-level patterns shared by the customer-web booking, post-booking and
 * account screens (Phase 12 rows 218-220).
 *
 * Deliberately a separate file from `components/ui.tsx`: that kit is
 * byte-identical across customer-web, admin-web and partner-web, so anything
 * added there has to be ported to all three. Everything here is
 * customer-only — a booking progress rail and an INR price breakdown have no
 * meaning in the admin or partner apps — so it lives outside the frozen kit.
 *
 * No component here contains a hex value or a raw `neutral-*`/`black/10`
 * class; every visual value resolves through the tokens in app/globals.css.
 */

/* -------------------------------------------------------------------------- */
/* Formatting                                                                 */
/* -------------------------------------------------------------------------- */

/**
 * Money, always to two decimals with the rupee sign attached.
 *
 * Kept as `toFixed(2)` rather than `Intl.NumberFormat` on purpose: the
 * amounts rendered here are echoed straight back from server-calculated
 * decimals, and a locale-dependent grouping separator would make the same
 * booking read differently depending on the browser's locale.
 */
export function inr(amount: number): string {
  return `₹${amount.toFixed(2)}`;
}

/**
 * A `YYYY-MM-DD` calendar date as "Mon, 4 Aug 2026".
 *
 * Parsed as `${iso}T00:00:00` — local midnight — rather than `new Date(iso)`,
 * which JS parses as *UTC* midnight and therefore renders as the previous day
 * for every timezone behind UTC. Same class of defect as the
 * `toISOString().slice(0, 10)` bug lib/date.ts exists to prevent, in the
 * opposite direction.
 */
export function formatCalendarDate(iso: string): string {
  if (!iso) return "";
  const date = new Date(`${iso}T00:00:00`);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleDateString(undefined, {
    weekday: "short",
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/** A ".NET TimeSpan" `hh:mm:ss` pair as "09:00–11:00". */
export function formatTimeRange(startTime: string, endTime: string): string {
  return `${startTime.slice(0, 5)}–${endTime.slice(0, 5)}`;
}

/** An ISO instant as a local date + time, for timeline and ledger rows. */
export function formatInstant(utc: string): string {
  const date = new Date(utc);
  if (Number.isNaN(date.getTime())) return utc;
  return date.toLocaleString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/**
 * An ISO instant as a local date alone, for values where the time of day is
 * noise: a billing date, the day a referral signed up.
 *
 * Separate from `formatCalendarDate`, which takes a `YYYY-MM-DD` and must not
 * be handed an instant — appending `T00:00:00` to one produces an unparseable
 * string and the raw ISO text leaks into the UI.
 */
export function formatInstantDate(utc: string): string {
  const date = new Date(utc);
  if (Number.isNaN(date.getTime())) return utc;
  return date.toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/* -------------------------------------------------------------------------- */
/* Status vocabulary                                                          */
/* -------------------------------------------------------------------------- */

/**
 * Maps a booking state onto the shared status token scale, so the same colour
 * means the same thing on the list, the detail page and the confirmation.
 * Money-back states read `info` rather than `danger` — a refund in flight is
 * not an error, it is the system working.
 */
export function bookingStatusTone(status: BookingStatus): BadgeTone {
  switch (status) {
    case BookingStatus.Completed:
    case BookingStatus.Confirmed:
      return "success";
    case BookingStatus.Assigned:
    case BookingStatus.InProgress:
      return "brand";
    case BookingStatus.PaymentPending:
    case BookingStatus.AwaitingFulfilment:
    case BookingStatus.Initiated:
    case BookingStatus.Rescheduled:
      return "warning";
    case BookingStatus.PaymentFailed:
    case BookingStatus.CancelledByCustomer:
    case BookingStatus.CancelledByAdmin:
      return "danger";
    case BookingStatus.RefundPending:
    case BookingStatus.Refunded:
      return "info";
    default:
      return "neutral";
  }
}

/** Status pill for a booking, using the server's own label text. */
export function BookingStatusBadge({
  status,
  label,
}: {
  status: BookingStatus;
  label: string;
}) {
  return <Badge tone={bookingStatusTone(status)}>{label}</Badge>;
}

/**
 * Customer-facing wording for a `BookingPartnerAssignment` row (task 208).
 * The booking's own status stays "Assigned" across both the offer and the
 * partner's accept, so this is the only signal that tells a customer their
 * professional has actually confirmed.
 */
export function partnerAssignmentLabel(status: BookingPartnerAssignmentStatus): string {
  switch (status) {
    case BookingPartnerAssignmentStatus.Assigned:
      return "Professional assigned — awaiting their confirmation";
    case BookingPartnerAssignmentStatus.Accepted:
      return "Professional confirmed";
    case BookingPartnerAssignmentStatus.Rejected:
      return "Professional declined — finding you another";
    case BookingPartnerAssignmentStatus.Reassigned:
      return "Reassigned to another professional";
    case BookingPartnerAssignmentStatus.Withdrawn:
      return "Assignment withdrawn — finding you another";
    default:
      return "Professional assignment updating";
  }
}

export function partnerAssignmentTone(status: BookingPartnerAssignmentStatus): BadgeTone {
  switch (status) {
    case BookingPartnerAssignmentStatus.Accepted:
      return "success";
    case BookingPartnerAssignmentStatus.Assigned:
      return "brand";
    case BookingPartnerAssignmentStatus.Rejected:
    case BookingPartnerAssignmentStatus.Withdrawn:
      return "warning";
    case BookingPartnerAssignmentStatus.Reassigned:
      return "info";
    default:
      return "neutral";
  }
}

export function recurringPlanStatusTone(status: RecurringBookingPlanStatus): BadgeTone {
  switch (status) {
    case RecurringBookingPlanStatus.Active:
      return "success";
    case RecurringBookingPlanStatus.Paused:
      return "warning";
    case RecurringBookingPlanStatus.Cancelled:
      return "danger";
    case RecurringBookingPlanStatus.Completed:
      return "neutral";
    default:
      return "neutral";
  }
}

export function supportStatusTone(status: SupportTicketStatus): BadgeTone {
  switch (status) {
    case SupportTicketStatus.Open:
      return "info";
    case SupportTicketStatus.InProgress:
      return "brand";
    case SupportTicketStatus.WaitingForCustomer:
      return "warning";
    case SupportTicketStatus.Escalated:
      return "danger";
    case SupportTicketStatus.Resolved:
      return "success";
    case SupportTicketStatus.Closed:
      return "neutral";
    default:
      return "neutral";
  }
}

/* -------------------------------------------------------------------------- */
/* Booking flow progress                                                      */
/* -------------------------------------------------------------------------- */

export const BOOKING_STEPS = ["Review", "Payment", "Confirmed"] as const;

/**
 * The three-step rail across booking summary → payment → success.
 *
 * An ordered list rather than a row of divs so the sequence is conveyed
 * without relying on the visual order, with `aria-current="step"` marking
 * where the customer is and a visually-hidden "Done"/"Current" suffix so the
 * state is not carried by colour alone.
 */
export function BookingProgress({ current }: { current: 0 | 1 | 2 }) {
  return (
    <nav aria-label="Booking progress" className="mb-6">
      <ol className="flex items-center gap-2 sm:gap-3">
        {BOOKING_STEPS.map((step, index) => {
          const isDone = index < current;
          const isCurrent = index === current;
          return (
            <li key={step} className="flex min-w-0 flex-1 items-center gap-2 sm:gap-3">
              <span
                aria-current={isCurrent ? "step" : undefined}
                className="flex min-w-0 items-center gap-2"
              >
                <span
                  aria-hidden
                  className={cx(
                    "flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold transition duration-fast ease-out",
                    isDone && "bg-brand-600 text-fg-on-brand shadow-brand",
                    isCurrent && "bg-brand-600 text-fg-on-brand shadow-brand ring-4 ring-brand-600/20",
                    !isDone && !isCurrent && "bg-surface-3 text-fg-subtle",
                  )}
                >
                  {isDone ? (
                    <svg
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="3"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      className="h-3.5 w-3.5"
                    >
                      <path d="m5 13 4 4L19 7" />
                    </svg>
                  ) : (
                    index + 1
                  )}
                </span>
                <span
                  className={cx(
                    "truncate text-xs font-medium sm:text-sm",
                    isCurrent ? "text-fg" : "text-fg-muted",
                  )}
                >
                  {step}
                  <span className="sr-only">
                    {isDone ? " — done" : isCurrent ? " — current step" : " — not started"}
                  </span>
                </span>
              </span>
              {index < BOOKING_STEPS.length - 1 ? (
                <span
                  aria-hidden
                  className={cx(
                    "h-px flex-1 rounded-full",
                    isDone ? "bg-brand-600" : "bg-line",
                  )}
                />
              ) : null}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

/* -------------------------------------------------------------------------- */
/* Money                                                                      */
/* -------------------------------------------------------------------------- */

/**
 * One line of a price breakdown. `tone` is the only thing that varies:
 * `muted` for contributing lines, `discount` for a saving, `total` for the
 * figure the customer is actually agreeing to pay.
 */
export function PriceLine({
  label,
  value,
  tone = "muted",
  hint,
}: {
  label: ReactNode;
  value: string;
  tone?: "muted" | "strong" | "discount" | "total";
  hint?: string;
}) {
  return (
    <div
      className={cx(
        "flex items-baseline justify-between gap-4",
        tone === "muted" && "text-sm text-fg-muted",
        tone === "strong" && "text-sm font-medium text-fg",
        tone === "discount" && "text-sm font-medium text-success",
        tone === "total" && "text-base font-semibold text-fg",
      )}
    >
      <dt className="min-w-0">
        <span className="break-words">{label}</span>
        {hint ? <span className="mt-0.5 block text-xs text-fg-subtle">{hint}</span> : null}
      </dt>
      <dd className={cx("nums shrink-0", tone === "total" && "text-lg")}>{value}</dd>
    </div>
  );
}

/**
 * The canonical price breakdown, shared by the booking summary, the payment
 * screen and the booking detail so the same booking never adds up differently
 * on two screens.
 *
 * Grouped rather than flat: what the service costs, then what is added on top
 * (tax, fees), then the single number that matters. `discount` is optional —
 * pass it and `total` becomes the post-discount payable.
 */
export function PriceBreakdownList({
  breakdown,
  discount,
  total,
  totalLabel = "Total payable",
}: {
  breakdown: PriceBreakdown;
  discount?: { code: string | null; amount: number } | null;
  total: number;
  totalLabel?: string;
}) {
  return (
    <dl className="flex flex-col gap-2.5">
      <PriceLine
        label={`Base price × ${breakdown.quantity}`}
        value={inr(breakdown.baseTotal)}
        tone="strong"
      />

      {breakdown.addOnLineItems.map((item) => (
        <PriceLine
          key={item.addOnId}
          label={`${item.name} × ${item.quantity}`}
          value={inr(item.lineTotal)}
        />
      ))}

      {breakdown.visitCharge > 0 ? (
        <PriceLine label="Visit charge" value={inr(breakdown.visitCharge)} />
      ) : null}

      <div className="my-1 border-t border-line" />

      <PriceLine label="Subtotal" value={inr(breakdown.subtotal)} tone="strong" />
      <PriceLine label={`Tax (${breakdown.taxPercentage}%)`} value={inr(breakdown.taxAmount)} />
      {breakdown.platformFee > 0 ? (
        <PriceLine label="Platform fee" value={inr(breakdown.platformFee)} />
      ) : null}

      {discount && discount.amount > 0 ? (
        <PriceLine
          label={discount.code ? `Coupon (${discount.code})` : "Discount"}
          value={`−${inr(discount.amount)}`}
          tone="discount"
        />
      ) : null}

      <div className="my-1 border-t border-line-strong" />

      <PriceLine label={totalLabel} value={inr(total)} tone="total" />
    </dl>
  );
}

/** Matching placeholder for `PriceBreakdownList` while the quote is in flight. */
export function PriceBreakdownSkeleton() {
  return (
    <div className="flex flex-col gap-3" aria-hidden>
      {[0, 1, 2].map((row) => (
        <div key={row} className="flex items-center justify-between gap-4">
          <Skeleton className="h-3.5 w-32" />
          <Skeleton className="h-3.5 w-16" />
        </div>
      ))}
      <div className="my-1 border-t border-line" />
      <div className="flex items-center justify-between gap-4">
        <Skeleton className="h-5 w-24" />
        <Skeleton className="h-5 w-24" />
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Layout helpers                                                             */
/* -------------------------------------------------------------------------- */

/**
 * Commit affordance that is inline on desktop and pinned to the bottom of the
 * viewport on mobile, where the summary column stacks below a long form and
 * the primary action would otherwise be several screens down.
 *
 * It renders its children exactly once — a second, separately-rendered mobile
 * button would duplicate every accessible name on the page and make "the Pay
 * button" ambiguous to both screen readers and tests. Pair it with
 * `STICKY_BAR_SPACER` on the page's `main` so the bar never covers content at
 * the end of a scroll.
 */
export function StickyActionBar({ children }: { children: ReactNode }) {
  return (
    <div
      className={cx(
        "fixed inset-x-0 bottom-0 z-40 flex flex-col gap-2 border-t border-line bg-surface/95 px-4 py-3 shadow-lg backdrop-blur",
        "supports-[padding:max(0px)]:pb-[max(0.75rem,env(safe-area-inset-bottom))]",
        "md:static md:z-auto md:flex-col md:gap-3 md:border-0 md:bg-transparent md:p-0 md:shadow-none md:backdrop-blur-none",
      )}
    >
      {children}
    </div>
  );
}

/** Bottom padding a page needs so `StickyActionBar` never covers its last row. */
export const STICKY_BAR_SPACER = "pb-40 md:pb-10";

/** Label/value row for the description lists on the detail and account screens. */
export function DetailRow({
  label,
  children,
  numeric = false,
}: {
  label: string;
  children: ReactNode;
  numeric?: boolean;
}) {
  return (
    <div className="flex items-baseline justify-between gap-4 text-sm">
      <dt className="shrink-0 text-fg-muted">{label}</dt>
      <dd className={cx("min-w-0 text-right font-medium text-fg", numeric && "nums")}>{children}</dd>
    </div>
  );
}

export function DetailList({ children }: { children: ReactNode }) {
  return <dl className="flex flex-col gap-2.5">{children}</dl>;
}

/**
 * Standard "this screen is loading" placeholder: a heading block plus a
 * configurable number of card-shaped blocks, sized to the real cards so the
 * page does not jump when the data lands.
 */
export function ScreenSkeleton({
  cards = 3,
  className = "mx-auto w-full max-w-3xl px-4 py-8 sm:px-6 sm:py-12",
  children,
}: {
  cards?: number;
  className?: string;
  /**
   * Optional live-region line rendered above the placeholder — for the cases
   * where the wait has a reason worth announcing (a redirect in flight), which
   * the silent `aria-hidden` blocks below cannot carry on their own.
   */
  children?: ReactNode;
}) {
  return (
    <main className={className}>
      {children ? <div className="mb-6">{children}</div> : null}
      <Skeleton className="h-8 w-56" />
      <Skeleton className="mt-3 h-4 w-72" />
      <div className="mt-8 flex flex-col gap-4">
        {Array.from({ length: cards }, (_, index) => (
          <Skeleton key={index} className="h-32 rounded-2xl" />
        ))}
      </div>
    </main>
  );
}
