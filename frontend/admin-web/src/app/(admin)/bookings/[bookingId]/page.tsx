"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { useState } from "react";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  PageHeading,
  Select,
  Skeleton,
  SkeletonText,
  Textarea,
} from "@/components/ui";
import {
  Breadcrumbs,
  ConfirmDialog,
  DescriptionList,
  FormActions,
  FormGrid,
  formatCurrency,
  formatDateTime,
} from "@/components/data-table";
import { BookingStatusBadge } from "@/components/status-badges";
import { describeError } from "@/lib/api";
import { useAdminClaims } from "@/lib/use-admin-claims";
import {
  cancelBooking,
  getBookingCompletionProof,
  getBookingDetail,
  refundBooking,
  rescheduleBooking,
  updateBookingStatus,
} from "@/lib/bookings-api";
import {
  CancellationActor,
  RefundMethod,
  RefundStatus,
  RescheduleActor,
} from "@/lib/bookings-types";
import { assignProviderToBooking, getBookingAssignmentHistory, rejectBookingAssignment } from "@/lib/providers-api";
import { BookingAssignedByType, BookingProviderAssignmentStatus } from "@/lib/providers-types";
import { BookingStatus } from "@/lib/types";

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

// Statuses reachable through the generic status-update action. Cancel/
// reschedule/refund each go through their own dedicated action below - the
// API rejects these five here regardless (see AdminBookingStatusUpdateRequest's
// doc comment), so they are left out of this picker entirely.
const GENERIC_STATUS_OPTIONS = [
  BookingStatus.Initiated,
  BookingStatus.PaymentPending,
  BookingStatus.PaymentFailed,
  BookingStatus.Confirmed,
  BookingStatus.AwaitingFulfilment,
  BookingStatus.Assigned,
  BookingStatus.InProgress,
  BookingStatus.Completed,
].map((value) => ({ value: String(value), label: BOOKING_STATUS_LABELS[value] }));

const CANCELLATION_ACTOR_LABELS: Record<CancellationActor, string> = {
  [CancellationActor.Customer]: "Customer",
  [CancellationActor.Admin]: "Admin",
  [CancellationActor.System]: "System",
};

const RESCHEDULE_ACTOR_LABELS: Record<RescheduleActor, string> = {
  [RescheduleActor.Customer]: "Customer",
  [RescheduleActor.Admin]: "Admin",
  [RescheduleActor.System]: "System",
};

const REFUND_STATUS_LABELS: Record<RefundStatus, string> = {
  [RefundStatus.Initiated]: "Initiated",
  [RefundStatus.Processing]: "Processing",
  [RefundStatus.Refunded]: "Refunded",
  [RefundStatus.Failed]: "Failed",
};

const REFUND_STATUS_TONES: Record<RefundStatus, "info" | "warning" | "success" | "danger"> = {
  [RefundStatus.Initiated]: "info",
  [RefundStatus.Processing]: "warning",
  [RefundStatus.Refunded]: "success",
  [RefundStatus.Failed]: "danger",
};

const ASSIGNMENT_STATUS_LABELS: Record<BookingProviderAssignmentStatus, string> = {
  [BookingProviderAssignmentStatus.Assigned]: "Awaiting response",
  [BookingProviderAssignmentStatus.Accepted]: "Accepted",
  [BookingProviderAssignmentStatus.Rejected]: "Rejected",
  [BookingProviderAssignmentStatus.Reassigned]: "Superseded (reassigned)",
  [BookingProviderAssignmentStatus.Withdrawn]: "Withdrawn (booking cancelled)",
};

/**
 * Admin booking detail screen (SRS 12.11.2-3, task 116): snapshots, full
 * status timeline, payment/cancellation/reschedule/refund history, and the
 * authorized cancel/reschedule/refund/status actions (tasks 115d, 117a-c).
 * Mutating actions are only shown to admins holding "bookings.write" - the
 * API enforces this server-side regardless, this purely avoids showing
 * controls that would just 403.
 *
 * Cancel, refund and assignment-rejection each go through `ConfirmDialog`
 * (task 222): they move money or break a scheduled job, and none of them were
 * previously confirmed at all.
 */
export default function BookingDetailPage() {
  const params = useParams<{ bookingId: string }>();
  const bookingId = params.bookingId;
  const claims = useAdminClaims();
  const canWrite = claims?.permissions.includes("bookings.write") ?? false;
  const queryClient = useQueryClient();

  const detailQuery = useQuery({
    queryKey: ["admin-booking-detail", bookingId],
    queryFn: () => getBookingDetail(bookingId),
  });

  const assignmentHistoryQuery = useQuery({
    queryKey: ["admin-booking-assignment-history", bookingId],
    queryFn: () => getBookingAssignmentHistory(bookingId),
  });

  const [actionError, setActionError] = useState<string | null>(null);
  const [actionNotice, setActionNotice] = useState<string | null>(null);

  const [newStatus, setNewStatus] = useState("");
  const [statusReason, setStatusReason] = useState("");

  const [cancelReason, setCancelReason] = useState("");
  const [cancelNotes, setCancelNotes] = useState("");
  const [confirmCancel, setConfirmCancel] = useState(false);

  const [rescheduleLocalityId, setRescheduleLocalityId] = useState("");
  const [rescheduleSlotWindowId, setRescheduleSlotWindowId] = useState("");
  const [rescheduleSlotDate, setRescheduleSlotDate] = useState("");
  const [rescheduleReason, setRescheduleReason] = useState("");

  const [refundIsFull, setRefundIsFull] = useState(true);
  const [refundAmount, setRefundAmount] = useState("");
  const [refundReason, setRefundReason] = useState("");
  const [refundMethod, setRefundMethod] = useState(String(RefundMethod.Gateway));
  const [confirmRefund, setConfirmRefund] = useState(false);

  const [assignProviderId, setAssignProviderId] = useState("");
  const [rejectReason, setRejectReason] = useState("");
  const [confirmReject, setConfirmReject] = useState(false);

  const invalidateDetail = () => {
    queryClient.invalidateQueries({ queryKey: ["admin-booking-detail", bookingId] });
    queryClient.invalidateQueries({ queryKey: ["admin-booking-assignment-history", bookingId] });
  };

  const statusMutation = useMutation({
    mutationFn: () => updateBookingStatus(bookingId, { newStatus: Number(newStatus) as BookingStatus, reason: statusReason || undefined }),
    onSuccess: () => {
      setActionError(null);
      setActionNotice("Status updated.");
      setStatusReason("");
      invalidateDetail();
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const cancelMutation = useMutation({
    mutationFn: () => cancelBooking(bookingId, { reason: cancelReason, internalNotes: cancelNotes || undefined }),
    onSuccess: () => {
      setActionError(null);
      setActionNotice("Booking cancelled.");
      setCancelReason("");
      setCancelNotes("");
      setConfirmCancel(false);
      invalidateDetail();
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const rescheduleMutation = useMutation({
    mutationFn: () =>
      rescheduleBooking(bookingId, {
        localityId: rescheduleLocalityId,
        slotWindowId: rescheduleSlotWindowId,
        slotDate: rescheduleSlotDate,
        reason: rescheduleReason || undefined,
      }),
    onSuccess: () => {
      setActionError(null);
      setActionNotice("Booking rescheduled.");
      setRescheduleLocalityId("");
      setRescheduleSlotWindowId("");
      setRescheduleSlotDate("");
      setRescheduleReason("");
      invalidateDetail();
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const refundMutation = useMutation({
    mutationFn: () =>
      refundBooking(bookingId, {
        isFullRefund: refundIsFull,
        amount: refundIsFull ? undefined : Number(refundAmount),
        reason: refundReason,
        method: Number(refundMethod) as RefundMethod,
      }),
    onSuccess: () => {
      setActionError(null);
      setActionNotice("Refund initiated.");
      setRefundAmount("");
      setRefundReason("");
      setConfirmRefund(false);
      invalidateDetail();
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const assignProviderMutation = useMutation({
    mutationFn: () => assignProviderToBooking(bookingId, { providerId: assignProviderId }),
    onSuccess: () => {
      setActionError(null);
      setActionNotice("Provider assigned.");
      setAssignProviderId("");
      invalidateDetail();
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const rejectAssignmentMutation = useMutation({
    mutationFn: () => rejectBookingAssignment(bookingId, { reason: rejectReason || undefined }),
    onSuccess: () => {
      setActionError(null);
      setActionNotice("Assignment rejected. Booking needs reassignment.");
      setRejectReason("");
      setConfirmReject(false);
      invalidateDetail();
    },
    onError: (err) => setActionError(describeError(err)),
  });

  if (detailQuery.isPending) {
    return <BookingDetailSkeleton />;
  }

  if (detailQuery.isError) {
    return (
      <div className="mx-auto w-full max-w-4xl">
        <PageHeading
          title="Booking"
          breadcrumbs={<Breadcrumbs items={[{ label: "Bookings", href: "/bookings" }, { label: "Detail" }]} />}
        />
        <Alert
          tone="error"
          title="Could not load this booking"
          action={
            <Button size="sm" variant="secondary" onClick={() => detailQuery.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(detailQuery.error)}
        </Alert>
      </div>
    );
  }

  const booking = detailQuery.data;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6">
      <PageHeading
        title={booking.customer.name}
        subtitle={`Booking ${booking.id}`}
        breadcrumbs={
          <Breadcrumbs items={[{ label: "Bookings", href: "/bookings" }, { label: booking.customer.name }]} />
        }
        actions={<BookingStatusBadge status={booking.status} label={booking.statusLabel} />}
      />

      {actionError ? <Alert tone="error">{actionError}</Alert> : null}
      {actionNotice ? <Alert tone="success">{actionNotice}</Alert> : null}

      <Card title="Booking summary" description={`Status: ${booking.statusLabel}`}>
        <DescriptionList
          columns={3}
          items={[
            { label: "Customer mobile", value: <span className="nums">{booking.customer.mobile}</span> },
            {
              label: "Slot",
              value: (
                <span className="nums">
                  {booking.slot.date} · {booking.slot.startTime}–{booking.slot.endTime}
                </span>
              ),
            },
            { label: "City", value: booking.address.city },
            { label: "Total payable", value: <span className="nums font-medium">{formatCurrency(booking.price.finalPayable)}</span> },
            { label: "Coupon", value: booking.price.couponCode ?? "—" },
            { label: "Created", value: formatDateTime(booking.createdAtUtc) },
          ]}
        />
      </Card>

      <Card title="Address" description="Snapshot at booking time">
        <p className="text-sm leading-relaxed text-fg">
          {[booking.address.label, booking.address.line1, booking.address.line2, booking.address.landmark, booking.address.city, booking.address.state, booking.address.pincode]
            .filter(Boolean)
            .join(", ")}
        </p>
        <p className="mt-1.5 text-sm text-fg-muted">
          Contact: {booking.address.contactName} · <span className="nums">{booking.address.contactMobile}</span>
        </p>
      </Card>

      <Card title="Services booked">
        <ul className="flex flex-col gap-2 text-sm">
          {booking.items.map((item) => (
            <li key={item.id} className="rounded-xl border border-line p-3">
              <div className="flex items-center justify-between gap-3">
                <span className="font-medium text-fg">{item.name}</span>
                <span className="nums text-fg">{formatCurrency(item.lineTotal)}</span>
              </div>
              {item.addOns.length > 0 ? (
                <ul className="mt-2 flex flex-col gap-1 pl-4 text-xs text-fg-muted">
                  {item.addOns.map((addOn) => (
                    <li key={addOn.id} className="flex items-center justify-between gap-3">
                      <span>{addOn.name}</span>
                      <span className="nums">{formatCurrency(addOn.lineTotal)}</span>
                    </li>
                  ))}
                </ul>
              ) : null}
            </li>
          ))}
        </ul>
      </Card>

      <Card title="Payment">
        {booking.payment === null ? (
          <p className="text-sm text-fg-muted">No payment transaction yet.</p>
        ) : (
          <DescriptionList
            columns={3}
            items={[
              {
                label: "Amount",
                value: (
                  <span className="nums">
                    {booking.payment.currency} {booking.payment.amount.toFixed(2)}
                  </span>
                ),
              },
              { label: "Gateway ref", value: booking.payment.gatewayPaymentRef ?? "—" },
              { label: "Updated", value: formatDateTime(booking.payment.updatedAtUtc) },
            ]}
          />
        )}
      </Card>

      {booking.status === BookingStatus.Completed ? <CompletionProofCard bookingId={booking.id} /> : null}

      <Card title="Status timeline" description="Full history (SRS 12.11.2-3)">
        <ol className="flex flex-col gap-2 text-sm">
          {booking.timeline.map((entry, index) => (
            <li key={index} className="rounded-xl border border-line p-3">
              <div className="flex items-center justify-between gap-3">
                <span className="font-medium text-fg">{entry.toStatusLabel}</span>
                <span className="text-xs text-fg-subtle">{formatDateTime(entry.changedAtUtc)}</span>
              </div>
              {entry.reason ? <p className="mt-1 text-xs text-fg-muted">{entry.reason}</p> : null}
            </li>
          ))}
        </ol>
      </Card>

      <Card
        title="Provider assignment"
        description="Manually assign a provider below, or the system auto-assigns the nearest eligible one when a booking reaches Awaiting Fulfilment (tasks 147, 159, 246)"
      >
        {assignmentHistoryQuery.isPending ? (
          <SkeletonText lines={3} />
        ) : assignmentHistoryQuery.isError ? (
          <Alert
            tone="error"
            action={
              <Button size="sm" variant="secondary" onClick={() => assignmentHistoryQuery.refetch()}>
                Retry
              </Button>
            }
          >
            {describeError(assignmentHistoryQuery.error)}
          </Alert>
        ) : assignmentHistoryQuery.data.length === 0 ? (
          <EmptyState
            title="No provider assigned yet"
            description={
              canWrite
                ? "Assign a provider below to put this booking into a professional's queue."
                : "An admin with booking write access can assign one."
            }
          />
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {assignmentHistoryQuery.data.map((assignment) => (
              <li key={assignment.id} className="rounded-xl border border-line p-3">
                <div className="flex items-center justify-between gap-3">
                  <span className="flex items-center gap-2">
                    <span className="font-medium text-fg">{assignment.providerDisplayName}</span>
                    {/* Task 249: lets ops tell an auto-assignment (task 246) from a manual admin pick at a glance. */}
                    <Badge tone={assignment.assignedByType === BookingAssignedByType.System ? "info" : "neutral"}>
                      {assignment.assignedByType === BookingAssignedByType.System ? "Auto-assigned" : "By admin"}
                    </Badge>
                  </span>
                  <Badge
                    tone={
                      assignment.status === BookingProviderAssignmentStatus.Accepted
                        ? "success"
                        : assignment.status === BookingProviderAssignmentStatus.Rejected
                          ? "danger"
                          : assignment.status === BookingProviderAssignmentStatus.Assigned
                            ? "info"
                            : "neutral"
                    }
                  >
                    {ASSIGNMENT_STATUS_LABELS[assignment.status]}
                  </Badge>
                </div>
                <p className="mt-1 text-xs text-fg-subtle">
                  Assigned {formatDateTime(assignment.assignedAt)}
                  {assignment.respondedAt ? ` · Responded ${formatDateTime(assignment.respondedAt)}` : ""}
                </p>
                {assignment.notes ? <p className="mt-1 text-xs text-fg-muted">{assignment.notes}</p> : null}
              </li>
            ))}
          </ul>
        )}

        {canWrite ? (
          <div className="mt-5 flex flex-col gap-4 border-t border-line pt-5">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
              <div className="flex-1">
                <Field
                  label="Provider ID to assign"
                  value={assignProviderId}
                  onChange={(e) => setAssignProviderId(e.target.value)}
                  placeholder="Provider GUID"
                />
              </div>
              <Button
                disabled={!assignProviderId.trim()}
                loading={assignProviderMutation.isPending}
                onClick={() => assignProviderMutation.mutate()}
              >
                Assign provider
              </Button>
            </div>

            <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
              <div className="flex-1">
                <Field
                  label="Rejection reason (optional)"
                  value={rejectReason}
                  onChange={(e) => setRejectReason(e.target.value)}
                  placeholder="Reason the current assignment is being rejected"
                />
              </div>
              <Button variant="danger" onClick={() => setConfirmReject(true)}>
                Reject current assignment
              </Button>
            </div>
          </div>
        ) : null}
      </Card>

      {booking.cancellation ? (
        <Card title="Cancellation">
          <DescriptionList
            columns={3}
            items={[
              { label: "Actor", value: CANCELLATION_ACTOR_LABELS[booking.cancellation.actor] },
              { label: "Fee", value: <span className="nums">{formatCurrency(booking.cancellation.cancellationFeeAmount)}</span> },
              { label: "Refund amount", value: <span className="nums">{formatCurrency(booking.cancellation.refundAmount)}</span> },
            ]}
          />
          <p className="mt-4 text-sm text-fg">{booking.cancellation.reason}</p>
          {booking.cancellation.internalNotes ? (
            <p className="mt-1 text-xs text-fg-subtle">Internal notes: {booking.cancellation.internalNotes}</p>
          ) : null}
        </Card>
      ) : null}

      {booking.reschedules.length > 0 ? (
        <Card title="Reschedule history">
          <ul className="flex flex-col gap-2 text-sm">
            {booking.reschedules.map((reschedule) => (
              <li key={reschedule.id} className="rounded-xl border border-line p-3">
                <div className="flex items-center justify-between gap-3">
                  <span className="nums text-fg">
                    {reschedule.fromSlotDate} {reschedule.fromSlotStartTime} → {reschedule.toSlotDate}{" "}
                    {reschedule.toSlotStartTime}
                  </span>
                  <span className="text-xs text-fg-subtle">{RESCHEDULE_ACTOR_LABELS[reschedule.actor]}</span>
                </div>
                {reschedule.feeAmount > 0 ? (
                  <p className="mt-1 text-xs text-fg-muted">Fee: {formatCurrency(reschedule.feeAmount)}</p>
                ) : null}
              </li>
            ))}
          </ul>
        </Card>
      ) : null}

      {booking.refunds.length > 0 ? (
        <Card title="Refund history">
          <ul className="flex flex-col gap-2 text-sm">
            {booking.refunds.map((refund) => (
              <li
                key={refund.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-line p-3"
              >
                <span className="min-w-0 flex-1 text-fg">{refund.reason}</span>
                <Badge tone={REFUND_STATUS_TONES[refund.status]}>{REFUND_STATUS_LABELS[refund.status]}</Badge>
                <span className="nums font-medium text-fg">{formatCurrency(refund.amount)}</span>
              </li>
            ))}
          </ul>
        </Card>
      ) : null}

      {canWrite ? (
        <>
          <Card title="Update status" description="General operational status transitions (task 115d)">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
              <div className="flex-1">
                <Select
                  label="New status"
                  options={[{ value: "", label: "Select a status…" }, ...GENERIC_STATUS_OPTIONS]}
                  value={newStatus}
                  onChange={(e) => setNewStatus(e.target.value)}
                />
              </div>
              <div className="flex-1">
                <Field label="Reason (optional)" value={statusReason} onChange={(e) => setStatusReason(e.target.value)} />
              </div>
              <Button disabled={!newStatus} loading={statusMutation.isPending} onClick={() => statusMutation.mutate()}>
                Update status
              </Button>
            </div>
          </Card>

          <Card title="Cancel booking" description="Admin-initiated cancellation (SRS 12.11.3, task 117a)">
            <div className="flex flex-col gap-4">
              <Field label="Reason" required value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} />
              <Textarea label="Internal notes (optional)" value={cancelNotes} onChange={(e) => setCancelNotes(e.target.value)} />
              <FormActions align="start">
                <Button variant="danger" disabled={!cancelReason.trim()} onClick={() => setConfirmCancel(true)}>
                  Cancel booking
                </Button>
              </FormActions>
            </div>
          </Card>

          <Card title="Reschedule booking" description="Admin-initiated reschedule (SRS 12.11.3, task 117b)">
            <div className="flex flex-col gap-4">
              <FormGrid>
                <Field label="Locality ID" required value={rescheduleLocalityId} onChange={(e) => setRescheduleLocalityId(e.target.value)} />
                <Field label="Slot window ID" required value={rescheduleSlotWindowId} onChange={(e) => setRescheduleSlotWindowId(e.target.value)} />
                <Field label="New slot date" type="date" required value={rescheduleSlotDate} onChange={(e) => setRescheduleSlotDate(e.target.value)} />
                <Field label="Reason (optional)" value={rescheduleReason} onChange={(e) => setRescheduleReason(e.target.value)} />
              </FormGrid>
              <FormActions align="start">
                <Button
                  disabled={!rescheduleLocalityId || !rescheduleSlotWindowId || !rescheduleSlotDate}
                  loading={rescheduleMutation.isPending}
                  onClick={() => rescheduleMutation.mutate()}
                >
                  Reschedule
                </Button>
              </FormActions>
            </div>
          </Card>

          <Card title="Refund" description="Full or partial refund with audit (SRS 12.11.3, 12.13.2-3, task 117c)">
            <div className="flex flex-col gap-4">
              <fieldset className="flex flex-wrap gap-4 text-sm text-fg">
                <legend className="mb-1.5 text-sm font-medium text-fg">Refund scope</legend>
                <label className="flex cursor-pointer items-center gap-2">
                  <input
                    type="radio"
                    name="refund-scope"
                    className="h-4 w-4 accent-brand-600"
                    checked={refundIsFull}
                    onChange={() => setRefundIsFull(true)}
                  />
                  Full refund
                </label>
                <label className="flex cursor-pointer items-center gap-2">
                  <input
                    type="radio"
                    name="refund-scope"
                    className="h-4 w-4 accent-brand-600"
                    checked={!refundIsFull}
                    onChange={() => setRefundIsFull(false)}
                  />
                  Partial refund
                </label>
              </fieldset>
              <FormGrid>
                {!refundIsFull ? (
                  <Field
                    label="Amount"
                    type="number"
                    min="0"
                    step="0.01"
                    required
                    leading="₹"
                    value={refundAmount}
                    onChange={(e) => setRefundAmount(e.target.value)}
                  />
                ) : null}
                <Select
                  label="Method"
                  options={[
                    { value: String(RefundMethod.Gateway), label: "Gateway" },
                    { value: String(RefundMethod.Wallet), label: "Wallet credit" },
                  ]}
                  value={refundMethod}
                  onChange={(e) => setRefundMethod(e.target.value)}
                />
                <Field label="Reason" required value={refundReason} onChange={(e) => setRefundReason(e.target.value)} />
              </FormGrid>
              <FormActions align="start">
                <Button
                  disabled={!refundReason.trim() || (!refundIsFull && !refundAmount)}
                  onClick={() => setConfirmRefund(true)}
                >
                  Initiate refund
                </Button>
              </FormActions>
            </div>
          </Card>
        </>
      ) : null}

      <ConfirmDialog
        open={confirmCancel}
        title="Cancel this booking?"
        description="The customer is notified and any cancellation fee is applied per policy."
        confirmLabel="Cancel booking"
        cancelLabel="Keep booking"
        loading={cancelMutation.isPending}
        error={cancelMutation.isError ? describeError(cancelMutation.error) : null}
        onCancel={() => setConfirmCancel(false)}
        onConfirm={() => cancelMutation.mutate()}
      >
        <p className="text-sm text-fg-muted">
          Reason: <span className="font-medium text-fg">{cancelReason}</span>
        </p>
      </ConfirmDialog>

      <ConfirmDialog
        open={confirmRefund}
        title={refundIsFull ? "Issue a full refund?" : "Issue a partial refund?"}
        description="Refunds are irreversible once the gateway accepts them."
        confirmLabel="Initiate refund"
        loading={refundMutation.isPending}
        error={refundMutation.isError ? describeError(refundMutation.error) : null}
        onCancel={() => setConfirmRefund(false)}
        onConfirm={() => refundMutation.mutate()}
      >
        <p className="text-sm text-fg-muted">
          Amount:{" "}
          <span className="nums font-medium text-fg">
            {refundIsFull ? formatCurrency(booking.price.finalPayable) : formatCurrency(Number(refundAmount) || 0)}
          </span>
        </p>
      </ConfirmDialog>

      <ConfirmDialog
        open={confirmReject}
        title="Reject the current assignment?"
        description="The booking returns to the unassigned queue and needs a new provider."
        confirmLabel="Reject assignment"
        loading={rejectAssignmentMutation.isPending}
        error={rejectAssignmentMutation.isError ? describeError(rejectAssignmentMutation.error) : null}
        onCancel={() => setConfirmReject(false)}
        onConfirm={() => rejectAssignmentMutation.mutate()}
      />
    </div>
  );
}

function BookingDetailSkeleton() {
  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6">
      <div>
        <Skeleton className="h-3 w-32" />
        <Skeleton className="mt-3 h-8 w-64" />
        <Skeleton className="mt-2 h-4 w-80" />
      </div>
      {Array.from({ length: 3 }, (_, index) => (
        <div key={index} className="rounded-2xl border border-line bg-surface p-6 shadow-sm">
          <Skeleton className="h-4 w-40" />
          <div className="mt-5">
            <SkeletonText lines={3} />
          </div>
        </div>
      ))}
    </div>
  );
}

/** Photo + checklist evidence the provider submitted at job completion - dispute-review evidence (tasks 195-198, SRS 12.11.2). */
function CompletionProofCard({ bookingId }: { bookingId: string }) {
  const query = useQuery({
    queryKey: ["admin-booking-completion-proof", bookingId],
    queryFn: () => getBookingCompletionProof(bookingId),
  });

  if (query.isPending) {
    return (
      <Card title="Completion proof">
        <SkeletonText lines={2} />
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Completion proof">
        <Alert
          tone="error"
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
    <Card title="Completion proof" description="Submitted by the provider at job completion (SRS 12.11.2)">
      <p className="text-sm text-fg-muted">
        Submitted {formatDateTime(proof.submittedAtUtc)} · {proof.photoRefs.length} photo(s)
      </p>
      {proof.photoRefs.length > 0 ? (
        <ul className="mt-2 flex flex-col gap-1 text-sm">
          {proof.photoRefs.map((ref, i) => (
            <li key={i} className="min-w-0 truncate">
              <a
                href={ref}
                target="_blank"
                rel="noreferrer"
                className="text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
              >
                {ref}
              </a>
            </li>
          ))}
        </ul>
      ) : null}
      {proof.checklistAnswers.length > 0 ? (
        <ul className="mt-3 flex flex-col gap-1 text-sm text-fg">
          {proof.checklistAnswers.map((answer, i) => (
            <li key={i}>
              <span className={answer.completed ? "text-success" : "text-fg-subtle"} aria-hidden>
                {answer.completed ? "✓" : "○"}
              </span>{" "}
              {answer.item}
              {answer.notes ? <span className="text-fg-muted"> — {answer.notes}</span> : null}
            </li>
          ))}
        </ul>
      ) : null}
    </Card>
  );
}
