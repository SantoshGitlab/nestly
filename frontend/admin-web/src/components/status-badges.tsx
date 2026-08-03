"use client";

import { Badge } from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { PartnerStatus } from "@/lib/partners-types";
import { BookingStatus, CustomerStatus, SupportTicketPriority, SupportTicketStatus } from "@/lib/types";

/**
 * Status → badge tone mapping for the admin's domain enums (task 222).
 *
 * Colour here is information, not decoration, so the mapping lives in one
 * place: a booking that reads `danger` in the list must read `danger` on the
 * detail screen and on the dashboard. Every tone is a semantic token
 * (`success`/`warning`/`danger`/`info`/`neutral`) so dark mode and any future
 * re-theme follow automatically.
 *
 * Labels are NOT produced here — each module already has a label helper
 * (`statusLabel` in lib/support.ts, `statusLabel` on the booking list item
 * from the API). These components take the text they should show.
 */

const BOOKING_STATUS_TONES: Record<BookingStatus, BadgeTone> = {
  [BookingStatus.Initiated]: "neutral",
  [BookingStatus.PaymentPending]: "warning",
  [BookingStatus.PaymentFailed]: "danger",
  [BookingStatus.Confirmed]: "info",
  [BookingStatus.AwaitingFulfilment]: "info",
  [BookingStatus.Assigned]: "info",
  [BookingStatus.InProgress]: "brand",
  [BookingStatus.Completed]: "success",
  [BookingStatus.CancelledByCustomer]: "danger",
  [BookingStatus.CancelledByAdmin]: "danger",
  [BookingStatus.Rescheduled]: "warning",
  [BookingStatus.RefundPending]: "warning",
  [BookingStatus.Refunded]: "neutral",
};

export function bookingStatusTone(status: BookingStatus): BadgeTone {
  return BOOKING_STATUS_TONES[status] ?? "neutral";
}

export function BookingStatusBadge({ status, label }: { status: BookingStatus; label: string }) {
  return <Badge tone={bookingStatusTone(status)}>{label}</Badge>;
}

const CUSTOMER_STATUS_TONES: Record<CustomerStatus, BadgeTone> = {
  [CustomerStatus.Active]: "success",
  [CustomerStatus.Blocked]: "danger",
  [CustomerStatus.Unverified]: "warning",
  [CustomerStatus.SoftDeleted]: "neutral",
};

const CUSTOMER_STATUS_LABELS: Record<CustomerStatus, string> = {
  [CustomerStatus.Active]: "Active",
  [CustomerStatus.Blocked]: "Blocked",
  [CustomerStatus.Unverified]: "Unverified",
  [CustomerStatus.SoftDeleted]: "Deleted",
};

export function CustomerStatusBadge({ status }: { status: CustomerStatus }) {
  return (
    <Badge tone={CUSTOMER_STATUS_TONES[status] ?? "neutral"}>
      {CUSTOMER_STATUS_LABELS[status] ?? "Unknown"}
    </Badge>
  );
}

const PARTNER_STATUS_TONES: Record<PartnerStatus, BadgeTone> = {
  [PartnerStatus.PendingVerification]: "warning",
  [PartnerStatus.Active]: "success",
  [PartnerStatus.Suspended]: "danger",
  [PartnerStatus.Deactivated]: "neutral",
};

export function PartnerStatusBadge({ status, label }: { status: PartnerStatus; label: string }) {
  return <Badge tone={PARTNER_STATUS_TONES[status] ?? "neutral"}>{label}</Badge>;
}

const TICKET_STATUS_TONES: Record<SupportTicketStatus, BadgeTone> = {
  [SupportTicketStatus.Open]: "info",
  [SupportTicketStatus.InProgress]: "brand",
  [SupportTicketStatus.WaitingForCustomer]: "warning",
  [SupportTicketStatus.Escalated]: "danger",
  [SupportTicketStatus.Resolved]: "success",
  [SupportTicketStatus.Closed]: "neutral",
};

export function TicketStatusBadge({ status, label }: { status: SupportTicketStatus; label: string }) {
  return <Badge tone={TICKET_STATUS_TONES[status] ?? "neutral"}>{label}</Badge>;
}

const TICKET_PRIORITY_TONES: Record<SupportTicketPriority, BadgeTone> = {
  [SupportTicketPriority.Low]: "neutral",
  [SupportTicketPriority.Normal]: "info",
  [SupportTicketPriority.High]: "warning",
  [SupportTicketPriority.Urgent]: "danger",
};

export function TicketPriorityBadge({
  priority,
  label,
}: {
  priority: SupportTicketPriority;
  label: string;
}) {
  return <Badge tone={TICKET_PRIORITY_TONES[priority] ?? "neutral"}>{label}</Badge>;
}
