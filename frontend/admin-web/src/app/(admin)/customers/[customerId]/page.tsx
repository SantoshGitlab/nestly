"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { Alert, Button, Card, Field, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import {
  BookingStatus,
  CustomerStatus,
  SupportTicketStatus,
  WalletEntryType,
} from "@/lib/types";
import type { AdminSessionClaims, CustomerDetail, CustomerNote } from "@/lib/types";

function useAdminClaims(): AdminSessionClaims | null {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);
  return claims;
}

const STATUS_LABELS: Record<CustomerStatus, string> = {
  [CustomerStatus.Active]: "Active",
  [CustomerStatus.Blocked]: "Blocked",
  [CustomerStatus.Unverified]: "Unverified",
  [CustomerStatus.SoftDeleted]: "Deleted",
};

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

const SUPPORT_STATUS_LABELS: Record<SupportTicketStatus, string> = {
  [SupportTicketStatus.Open]: "Open",
  [SupportTicketStatus.InProgress]: "In Progress",
  [SupportTicketStatus.WaitingForCustomer]: "Waiting for Customer",
  [SupportTicketStatus.Escalated]: "Escalated",
  [SupportTicketStatus.Resolved]: "Resolved",
  [SupportTicketStatus.Closed]: "Closed",
};

/**
 * Customer 360 view (SRS 12.4.2, task 102): profile, addresses, bookings,
 * wallet, coupons, support tickets and notes, plus block/unblock and
 * add-note actions (SRS 12.4.3, tasks 101c-101d). Mutating actions are only
 * shown to admins holding "customers.write" - the API enforces this
 * server-side regardless, this is purely to avoid showing controls that
 * would just 403.
 */
export default function CustomerDetailPage() {
  const params = useParams<{ customerId: string }>();
  const customerId = params.customerId;
  const claims = useAdminClaims();
  const canWrite = claims?.permissions.includes("customers.write") ?? false;
  const queryClient = useQueryClient();

  const detailQuery = useQuery({
    queryKey: ["admin-customer-detail", customerId],
    queryFn: () => apiFetch<CustomerDetail>(`${API_V1}/customers/${customerId}`, { authenticated: true }),
  });

  const [blockReason, setBlockReason] = useState("");
  const [noteText, setNoteText] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);

  const invalidateDetail = () =>
    queryClient.invalidateQueries({ queryKey: ["admin-customer-detail", customerId] });

  const blockMutation = useMutation({
    mutationFn: (reason: string) =>
      apiFetch<CustomerDetail>(`${API_V1}/customers/${customerId}/block`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ reason }),
      }),
    onSuccess: () => {
      setBlockReason("");
      setActionError(null);
      invalidateDetail();
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const unblockMutation = useMutation({
    mutationFn: () =>
      apiFetch<CustomerDetail>(`${API_V1}/customers/${customerId}/unblock`, {
        method: "POST",
        authenticated: true,
      }),
    onSuccess: () => {
      setActionError(null);
      invalidateDetail();
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const addNoteMutation = useMutation({
    mutationFn: (note: string) =>
      apiFetch<CustomerNote>(`${API_V1}/customers/${customerId}/notes`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ note }),
      }),
    onSuccess: () => {
      setNoteText("");
      setActionError(null);
      invalidateDetail();
    },
    onError: (err) => setActionError(describeError(err)),
  });

  if (detailQuery.isPending) {
    return <p className="text-sm text-neutral-500">Loading customer…</p>;
  }

  if (detailQuery.isError) {
    return <Alert>{describeError(detailQuery.error)}</Alert>;
  }

  const customer = detailQuery.data;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6">
      <div className="flex items-center justify-between">
        <PageHeading title={customer.name} subtitle={`${customer.mobile}${customer.email ? ` · ${customer.email}` : ""}`} />
        <Link href="/customers" className="text-sm underline-offset-2 hover:underline">
          Back to customers
        </Link>
      </div>

      {actionError ? <Alert>{actionError}</Alert> : null}

      <Card title="Profile" description={`Status: ${STATUS_LABELS[customer.status]}`}>
        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm sm:grid-cols-3">
          <div>
            <dt className="text-neutral-500">City</dt>
            <dd>{customer.city ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-neutral-500">State</dt>
            <dd>{customer.state ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-neutral-500">Pincode</dt>
            <dd>{customer.pincode ?? "—"}</dd>
          </div>
          <div>
            <dt className="text-neutral-500">Registered</dt>
            <dd>{new Date(customer.createdAtUtc).toLocaleDateString()}</dd>
          </div>
          <div>
            <dt className="text-neutral-500">Wallet balance</dt>
            <dd>₹{customer.walletBalance.toFixed(2)}</dd>
          </div>
        </dl>

        {canWrite ? (
          <div className="mt-5 flex flex-col gap-3 border-t border-black/10 pt-5 dark:border-white/15">
            {customer.status === CustomerStatus.Blocked ? (
              <Button
                variant="secondary"
                onClick={() => unblockMutation.mutate()}
                disabled={unblockMutation.isPending}
              >
                {unblockMutation.isPending ? "Unblocking…" : "Unblock customer"}
              </Button>
            ) : (
              <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
                <div className="flex-1">
                  <Field
                    label="Block reason"
                    value={blockReason}
                    onChange={(e) => setBlockReason(e.target.value)}
                    placeholder="Reason for blocking this account"
                  />
                </div>
                <Button
                  variant="danger"
                  disabled={!blockReason.trim() || blockMutation.isPending}
                  onClick={() => blockMutation.mutate(blockReason.trim())}
                >
                  {blockMutation.isPending ? "Blocking…" : "Block customer"}
                </Button>
              </div>
            )}
          </div>
        ) : null}
      </Card>

      <Card title="Addresses" description="SRS 12.4.2">
        {customer.addresses.length === 0 ? (
          <p className="text-sm text-neutral-500">No saved addresses.</p>
        ) : (
          <ul className="flex flex-col gap-3 text-sm">
            {customer.addresses.map((address) => (
              <li key={address.id} className="rounded-lg border border-black/10 p-3 dark:border-white/15">
                <p className="font-medium">
                  {address.label} {address.isDefault ? "(default)" : ""}
                </p>
                <p className="text-neutral-600 dark:text-neutral-400">
                  {[address.line1, address.line2, address.landmark, address.city, address.state, address.pincode]
                    .filter(Boolean)
                    .join(", ")}
                </p>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card title="Booking history" description="SRS 12.4.2">
        {customer.bookings.length === 0 ? (
          <p className="text-sm text-neutral-500">No bookings yet.</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {customer.bookings.map((booking) => (
              <li
                key={booking.id}
                className="flex items-center justify-between rounded-lg border border-black/10 p-3 dark:border-white/15"
              >
                <span>{booking.slotDate}</span>
                <span>{BOOKING_STATUS_LABELS[booking.status]}</span>
                <span className="font-medium">₹{booking.totalPayableSnapshot.toFixed(2)}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card title="Wallet / refund history" description={`Current balance: ₹${customer.walletBalance.toFixed(2)}`}>
        {customer.walletEntries.length === 0 ? (
          <p className="text-sm text-neutral-500">No wallet activity.</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {customer.walletEntries.map((entry) => (
              <li
                key={entry.id}
                className="flex items-center justify-between rounded-lg border border-black/10 p-3 dark:border-white/15"
              >
                <span>{entry.description}</span>
                <span>{new Date(entry.createdAtUtc).toLocaleDateString()}</span>
                <span className={entry.entryType === WalletEntryType.Credit ? "text-green-700 dark:text-green-400" : "text-red-700 dark:text-red-400"}>
                  {entry.entryType === WalletEntryType.Credit ? "+" : "-"}₹{entry.amount.toFixed(2)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card title="Coupons used" description="SRS 12.4.2">
        {customer.coupons.length === 0 ? (
          <p className="text-sm text-neutral-500">No coupons redeemed.</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {customer.coupons.map((coupon, index) => (
              <li
                key={`${coupon.couponId}-${coupon.bookingId}-${index}`}
                className="flex items-center justify-between rounded-lg border border-black/10 p-3 dark:border-white/15"
              >
                <span className="font-mono">{coupon.couponCode}</span>
                <span>{new Date(coupon.redeemedAtUtc).toLocaleDateString()}</span>
                <span>-₹{coupon.discountAmount.toFixed(2)}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card title="Support tickets" description="SRS 12.4.2">
        {customer.supportTickets.length === 0 ? (
          <p className="text-sm text-neutral-500">No support tickets.</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {customer.supportTickets.map((ticket) => (
              <li
                key={ticket.id}
                className="flex items-center justify-between rounded-lg border border-black/10 p-3 dark:border-white/15"
              >
                <span>{ticket.subject}</span>
                <span>{SUPPORT_STATUS_LABELS[ticket.status]}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card title="Internal notes" description="SRS 12.4.2, 12.4.3">
        {canWrite ? (
          <div className="mb-4 flex flex-col gap-2 sm:flex-row sm:items-end">
            <div className="flex-1">
              <Field label="Add a note" value={noteText} onChange={(e) => setNoteText(e.target.value)} />
            </div>
            <Button
              disabled={!noteText.trim() || addNoteMutation.isPending}
              onClick={() => addNoteMutation.mutate(noteText.trim())}
            >
              {addNoteMutation.isPending ? "Saving…" : "Add note"}
            </Button>
          </div>
        ) : null}

        {customer.notes.length === 0 ? (
          <p className="text-sm text-neutral-500">No notes yet.</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {customer.notes.map((note) => (
              <li key={note.id} className="rounded-lg border border-black/10 p-3 dark:border-white/15">
                <p>{note.note}</p>
                <p className="mt-1 text-xs text-neutral-500">{new Date(note.createdAtUtc).toLocaleString()}</p>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}
