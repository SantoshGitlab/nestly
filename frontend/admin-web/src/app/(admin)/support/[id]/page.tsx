"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Alert, Button, Card, Field, PageHeading, Textarea } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import {
  assignSupportTicket,
  closeSupportTicket,
  escalateSupportTicket,
  getSupportTicket,
  linkSupportTicketBooking,
  listAssignableAdmins,
  resolveSupportTicket,
  respondToSupportTicket,
  unassignSupportTicket,
} from "@/lib/support-api";
import { categoryLabel, priorityLabel, statusLabel } from "@/lib/support";
import { SupportTicketCommentAuthorType } from "@/lib/support-types";
import type { AdminSupportTicketDetailResponse, AssignableAdminResponse } from "@/lib/support-types";
import { BookingStatus, SupportTicketStatus } from "@/lib/types";
import type { AdminSessionClaims } from "@/lib/types";

const selectClassName =
  "rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white";
const labelClassName = "flex flex-col gap-1.5 text-sm font-medium";

const BOOKING_STATUS_LABELS: Record<BookingStatus, string> = {
  [BookingStatus.Initiated]: "Booking Started",
  [BookingStatus.PaymentPending]: "Awaiting Payment",
  [BookingStatus.PaymentFailed]: "Payment Failed",
  [BookingStatus.Confirmed]: "Confirmed",
  [BookingStatus.AwaitingFulfilment]: "Preparing Service",
  [BookingStatus.Assigned]: "Professional Assigned",
  [BookingStatus.InProgress]: "In Progress",
  [BookingStatus.Completed]: "Completed",
  [BookingStatus.CancelledByCustomer]: "Cancelled by Customer",
  [BookingStatus.CancelledByAdmin]: "Cancelled by Admin",
  [BookingStatus.Rescheduled]: "Rescheduled",
  [BookingStatus.RefundPending]: "Refund in Progress",
  [BookingStatus.Refunded]: "Refunded",
};

const COMMENT_AUTHOR_LABELS: Record<SupportTicketCommentAuthorType, string> = {
  [SupportTicketCommentAuthorType.Customer]: "Customer",
  [SupportTicketCommentAuthorType.Support]: "Support",
  [SupportTicketCommentAuthorType.System]: "System",
};

function useAdminClaims(): AdminSessionClaims | null {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);
  return claims;
}

/**
 * Same three lifecycle facts `SupportTicketLifecycle` (backend, SRS 31.2)
 * enforces server-side, mirrored here only to hide obviously-invalid action
 * buttons - the API is the real gate (every mutation still 422s on an
 * invalid transition regardless of what this hides).
 */
function canEscalate(status: SupportTicketStatus): boolean {
  return (
    status === SupportTicketStatus.Open ||
    status === SupportTicketStatus.InProgress ||
    status === SupportTicketStatus.WaitingForCustomer
  );
}

function canResolve(status: SupportTicketStatus): boolean {
  return (
    status === SupportTicketStatus.InProgress ||
    status === SupportTicketStatus.WaitingForCustomer ||
    status === SupportTicketStatus.Escalated
  );
}

function canClose(status: SupportTicketStatus): boolean {
  return status === SupportTicketStatus.Open || status === SupportTicketStatus.Resolved;
}

/**
 * Admin ticket workflow detail screen (SRS 12.14.2, 16.3, tasks 120a-121):
 * comment thread, assignee, and linked booking summary, plus the full
 * assign/unassign, respond, escalate, resolve/close and link-booking action
 * set `SupportTicketsController` exposes. Mirrors
 * `(admin)/customers/[customerId]/page.tsx`'s detail-screen-plus-mutations
 * shape. Mutating actions are only shown to admins holding "support.write" -
 * the API enforces this server-side regardless, this is purely to avoid
 * showing controls that would just 403.
 */
export default function SupportTicketDetailPage() {
  const params = useParams<{ id: string }>();
  const ticketId = params.id;
  const claims = useAdminClaims();
  const canWrite = claims?.permissions.includes("support.write") ?? false;
  const queryClient = useQueryClient();

  const [assigneeId, setAssigneeId] = useState("");
  const [responseText, setResponseText] = useState("");
  const [resolutionSummary, setResolutionSummary] = useState("");
  const [bookingIdDraft, setBookingIdDraft] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);

  const detailQuery = useQuery({
    queryKey: ["admin-support-ticket-detail", ticketId],
    queryFn: () => getSupportTicket(ticketId),
  });

  const assignableAdminsQuery = useQuery({
    queryKey: ["admin-support-ticket-assignable-admins"],
    queryFn: () => listAssignableAdmins(),
    enabled: canWrite,
  });

  // Each mutation returns the ticket's full updated detail response, so its
  // onSuccess below writes that straight into the query cache rather than
  // invalidating and re-fetching - same shortcut ReviewModerationPage skips
  // (it invalidates instead) because its mutations return a single row, not
  // the full detail shape this screen already has in hand.
  function useTicketMutation<TArgs>(mutationFn: (args: TArgs) => Promise<AdminSupportTicketDetailResponse>) {
    return useMutation({
      mutationFn,
      onSuccess: (data) => {
        setActionError(null);
        queryClient.setQueryData(["admin-support-ticket-detail", ticketId], data);
      },
      onError: (err) => setActionError(describeError(err)),
    });
  }

  const assignMutation = useTicketMutation((adminUserId: string) => assignSupportTicket(ticketId, { adminUserId }));
  const unassignMutation = useTicketMutation<void>(() => unassignSupportTicket(ticketId));
  const respondMutation = useTicketMutation((comment: string) => respondToSupportTicket(ticketId, { comment }));
  const escalateMutation = useTicketMutation<void>(() => escalateSupportTicket(ticketId));
  const resolveMutation = useTicketMutation((summary: string) => resolveSupportTicket(ticketId, { resolutionSummary: summary }));
  const closeMutation = useTicketMutation<void>(() => closeSupportTicket(ticketId));
  const linkBookingMutation = useTicketMutation((bookingId: string) => linkSupportTicketBooking(ticketId, { bookingId }));

  const onAssign = (event: FormEvent) => {
    event.preventDefault();
    if (!assigneeId) return;
    // No need to reset `assigneeId` on success: once assigned, this form is
    // replaced by the "assigned to X / Unassign" view below, so there is
    // nothing left for the leftover draft value to affect.
    assignMutation.mutate(assigneeId);
  };

  const onRespond = (event: FormEvent) => {
    event.preventDefault();
    const comment = responseText.trim();
    if (!comment) return;
    respondMutation.mutate(comment);
    setResponseText("");
  };

  const onResolve = (event: FormEvent) => {
    event.preventDefault();
    const summary = resolutionSummary.trim();
    if (!summary) return;
    resolveMutation.mutate(summary);
    setResolutionSummary("");
  };

  const onLinkBooking = (event: FormEvent) => {
    event.preventDefault();
    const bookingId = bookingIdDraft.trim();
    if (!bookingId) return;
    linkBookingMutation.mutate(bookingId);
    setBookingIdDraft("");
  };

  if (detailQuery.isPending) {
    return <p className="text-sm text-neutral-500">Loading ticket…</p>;
  }

  if (detailQuery.isError) {
    return <Alert>{describeError(detailQuery.error)}</Alert>;
  }

  const ticket = detailQuery.data;
  const isClosed = ticket.status === SupportTicketStatus.Closed;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6">
      <div className="flex items-center justify-between">
        <PageHeading
          title={ticket.subject}
          subtitle={`${ticket.customerName} · ${categoryLabel(ticket.category)} · ${priorityLabel(ticket.priority)} · ${statusLabel(ticket.status)}`}
        />
        <Link href="/support" className="text-sm underline-offset-2 hover:underline">
          Back to tickets
        </Link>
      </div>

      {actionError ? <Alert>{actionError}</Alert> : null}

      <Card title="Ticket details">
        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm sm:grid-cols-3">
          <div>
            <dt className="text-neutral-500">Status</dt>
            <dd>{statusLabel(ticket.status)}</dd>
          </div>
          <div>
            <dt className="text-neutral-500">Disputed</dt>
            <dd>{ticket.isDisputed ? "Yes" : "No"}</dd>
          </div>
          <div>
            <dt className="text-neutral-500">Assigned to</dt>
            <dd>{ticket.assignedAdminName ?? "Unassigned"}</dd>
          </div>
          <div>
            <dt className="text-neutral-500">Escalated at</dt>
            <dd>{ticket.escalatedAtUtc ? new Date(ticket.escalatedAtUtc).toLocaleString() : "—"}</dd>
          </div>
          <div>
            <dt className="text-neutral-500">Created</dt>
            <dd>{new Date(ticket.createdAtUtc).toLocaleString()}</dd>
          </div>
          <div>
            <dt className="text-neutral-500">Last updated</dt>
            <dd>{new Date(ticket.updatedAtUtc).toLocaleString()}</dd>
          </div>
        </dl>

        <p className="mt-4 text-sm">{ticket.description}</p>

        {ticket.resolutionSummary ? (
          <p className="mt-3 rounded-lg bg-black/5 px-3 py-2 text-xs text-neutral-700 dark:bg-white/5 dark:text-neutral-300">
            Resolution: {ticket.resolutionSummary}
          </p>
        ) : null}
      </Card>

      <Card title="Linked booking" description="SRS 12.14.2 &quot;Link ... booking action&quot;, task 120e">
        {ticket.booking ? (
          <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm sm:grid-cols-3">
            <div>
              <dt className="text-neutral-500">Booking date</dt>
              <dd>{ticket.booking.slotDate}</dd>
            </div>
            <div>
              <dt className="text-neutral-500">Slot</dt>
              <dd>
                {ticket.booking.slotStartTimeSnapshot}–{ticket.booking.slotEndTimeSnapshot}
              </dd>
            </div>
            <div>
              <dt className="text-neutral-500">Status</dt>
              <dd>{BOOKING_STATUS_LABELS[ticket.booking.status]}</dd>
            </div>
            <div>
              <dt className="text-neutral-500">Total payable</dt>
              <dd>₹{ticket.booking.totalPayableSnapshot.toFixed(2)}</dd>
            </div>
          </dl>
        ) : (
          <p className="text-sm text-neutral-500">No booking linked to this ticket.</p>
        )}

        {canWrite ? (
          <form onSubmit={onLinkBooking} className="mt-4 flex flex-col gap-2 border-t border-black/10 pt-4 dark:border-white/15 sm:flex-row sm:items-end">
            <div className="flex-1">
              <Field
                label={ticket.booking ? "Re-link to a different booking" : "Booking ID"}
                placeholder="Booking GUID"
                value={bookingIdDraft}
                onChange={(e) => setBookingIdDraft(e.target.value)}
              />
            </div>
            <Button type="submit" variant="secondary" disabled={!bookingIdDraft.trim() || linkBookingMutation.isPending}>
              {linkBookingMutation.isPending ? "Linking…" : "Link booking"}
            </Button>
          </form>
        ) : null}
      </Card>

      {canWrite ? (
        <Card title="Assignment" description="SRS 12.14.2 &quot;Assign to team/user&quot;, task 120a">
          {ticket.assignedAdminName ? (
            <div className="flex items-center justify-between text-sm">
              <span>
                Assigned to <strong>{ticket.assignedAdminName}</strong>
              </span>
              <Button variant="secondary" disabled={isClosed || unassignMutation.isPending} onClick={() => unassignMutation.mutate()}>
                {unassignMutation.isPending ? "Unassigning…" : "Unassign"}
              </Button>
            </div>
          ) : (
            <form onSubmit={onAssign} className="flex flex-col gap-2 sm:flex-row sm:items-end">
              <label className={`${labelClassName} flex-1`}>
                Assign to
                <select className={selectClassName} value={assigneeId} onChange={(e) => setAssigneeId(e.target.value)}>
                  <option value="" disabled>
                    Select an admin…
                  </option>
                  {(assignableAdminsQuery.data ?? []).map((admin: AssignableAdminResponse) => (
                    <option key={admin.id} value={admin.id}>
                      {admin.fullName} ({admin.email})
                    </option>
                  ))}
                </select>
              </label>
              <Button type="submit" disabled={isClosed || !assigneeId || assignMutation.isPending}>
                {assignMutation.isPending ? "Assigning…" : "Assign"}
              </Button>
            </form>
          )}
        </Card>
      ) : null}

      {canWrite ? (
        <Card title="Workflow actions" description="SRS 12.14.2 &quot;Mark escalated&quot; / &quot;Mark resolved/closed&quot;, tasks 120c-120d">
          <div className="flex flex-wrap gap-2">
            <Button variant="secondary" disabled={!canEscalate(ticket.status) || escalateMutation.isPending} onClick={() => escalateMutation.mutate()}>
              {escalateMutation.isPending ? "Escalating…" : "Escalate"}
            </Button>
            <Button variant="secondary" disabled={!canClose(ticket.status) || closeMutation.isPending} onClick={() => closeMutation.mutate()}>
              {closeMutation.isPending ? "Closing…" : "Close"}
            </Button>
          </div>

          {canResolve(ticket.status) ? (
            <form onSubmit={onResolve} className="mt-4 flex flex-col gap-2 border-t border-black/10 pt-4 dark:border-white/15">
              <Textarea
                label="Resolution summary"
                value={resolutionSummary}
                onChange={(e) => setResolutionSummary(e.target.value)}
                placeholder="Describe how this ticket was resolved"
              />
              <Button type="submit" disabled={!resolutionSummary.trim() || resolveMutation.isPending} className="self-start">
                {resolveMutation.isPending ? "Resolving…" : "Mark resolved"}
              </Button>
            </form>
          ) : null}
        </Card>
      ) : null}

      <Card title="Comment thread" description="SRS 12.14.2 &quot;Add response/note&quot;, task 120b">
        {ticket.comments.length === 0 ? (
          <p className="text-sm text-neutral-500">No comments yet.</p>
        ) : (
          <ul className="flex flex-col gap-3 text-sm">
            {ticket.comments.map((comment) => (
              <li key={comment.id} className="rounded-lg border border-black/10 p-3 dark:border-white/15">
                <div className="flex items-center justify-between">
                  <span className="font-medium">{COMMENT_AUTHOR_LABELS[comment.authorType]}</span>
                  <span className="text-xs text-neutral-500">{new Date(comment.createdAt).toLocaleString()}</span>
                </div>
                <p className="mt-1">{comment.comment}</p>
              </li>
            ))}
          </ul>
        )}

        {canWrite && !isClosed ? (
          <form onSubmit={onRespond} className="mt-4 flex flex-col gap-2 border-t border-black/10 pt-4 dark:border-white/15">
            <Textarea
              label="Add a response"
              value={responseText}
              onChange={(e) => setResponseText(e.target.value)}
              placeholder="Reply to the customer"
            />
            <Button type="submit" disabled={!responseText.trim() || respondMutation.isPending} className="self-start">
              {respondMutation.isPending ? "Sending…" : "Send response"}
            </Button>
          </form>
        ) : null}
      </Card>
    </div>
  );
}
