"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { Reveal } from "@/components/motion";
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
  StatTile,
} from "@/components/ui";
import { Pagination, formatDate } from "@/components/data-table";
import { describeError } from "@/lib/api";
import { assignProviderToBooking, getEligibleProviders } from "@/lib/providers-api";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { BookingsTabs } from "../_components/BookingsTabs";
import {
  ASSIGNED_BY_LABELS,
  ASSIGNMENT_STATUS_LABELS,
  AssignmentStatus,
  formatSlotTime,
  getBookingConflictCount,
  searchBookingConflicts,
} from "../_lib/conflicts-api";
import type { BookingConflictGroup, ConflictedBooking } from "../_lib/conflicts-api";

const PAGE_SIZE = 10;

/**
 * Conflicted orders: bookings one provider is live on at overlapping times
 * (tasks 321/322).
 *
 * Two things about the shape of this screen are deliberate.
 *
 * First, the unit of work is the **group**, not the booking. A clash is
 * resolved by moving one booking out of the overlap, so showing a flat list of
 * offending bookings would hide the only thing that makes the decision
 * possible - what each one collides with, and which of them the provider has
 * actually accepted.
 *
 * Second, reassignment posts to the existing
 * `POST /admin/bookings/{id}/assign-provider`, not to a "resolve conflict"
 * endpoint of this screen's own. That keeps one write path to assignment
 * state, so a fix made here gets the same validation, the same supersede
 * handling, the same task 288 conflict check and the same audit trail as an
 * assignment made anywhere else. It also means the fix is verified by the
 * invariant itself: a reassignment that would leave the provider still
 * double-booked is rejected by the server.
 */
export default function BookingConflictsPage() {
  const claims = useAdminClaims();
  const canWrite = claims?.permissions.includes("bookings.write") ?? false;
  const queryClient = useQueryClient();

  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [appliedFrom, setAppliedFrom] = useState("");
  const [appliedTo, setAppliedTo] = useState("");
  const [page, setPage] = useState(1);

  const [actionError, setActionError] = useState<string | null>(null);
  const [actionNotice, setActionNotice] = useState<string | null>(null);

  const countQuery = useQuery({
    queryKey: ["booking-conflicts", "count", appliedFrom] as const,
    queryFn: () => getBookingConflictCount(appliedFrom || undefined),
  });

  const listQuery = useQuery({
    queryKey: ["booking-conflicts", "list", appliedFrom, appliedTo, page] as const,
    queryFn: () =>
      searchBookingConflicts({
        fromDate: appliedFrom || undefined,
        toDate: appliedTo || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: keepPreviousData,
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ["booking-conflicts"] });
  };

  const onApply = () => {
    setAppliedFrom(fromDate);
    setAppliedTo(toDate);
    setPage(1);
  };

  const onClear = () => {
    setFromDate("");
    setToDate("");
    setAppliedFrom("");
    setAppliedTo("");
    setPage(1);
  };

  const groups = listQuery.data?.items ?? [];
  const totalCount = listQuery.data?.totalCount ?? 0;

  return (
    <div className="space-y-6">
      <PageHeading
        title="Assignment conflicts"
        subtitle="Bookings where one provider is committed to two overlapping jobs. Reassign one of them to clear the clash."
      />
      <BookingsTabs />

      <div className="grid gap-4 sm:grid-cols-2">
        <StatTile
          label="Open conflicts"
          value={countQuery.isPending ? "—" : String(countQuery.data?.conflictCount ?? 0)}
          hint="Provider-days with overlapping live jobs, from the start date onward"
        />
        <StatTile
          label="Shown in this range"
          value={listQuery.isPending ? "—" : String(totalCount)}
          hint="Matching the filter below"
        />
      </div>

      <Card>
        <div className="flex flex-wrap items-end gap-3">
          <Field
            label="From date"
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
            hint="Defaults to today — past clashes can no longer be fixed by moving anyone"
          />
          <Field label="To date" type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
          <Button onClick={onApply}>Apply</Button>
          <Button variant="secondary" onClick={onClear}>
            Clear
          </Button>
        </div>
      </Card>

      {actionError ? <Alert tone="error">{actionError}</Alert> : null}
      {actionNotice ? <Alert tone="success">{actionNotice}</Alert> : null}
      {!canWrite ? (
        <Alert tone="info">
          You can review conflicts but not resolve them — reassigning a booking needs the
          &quot;bookings.write&quot; permission.
        </Alert>
      ) : null}

      {listQuery.isPending ? (
        <Card>
          <Skeleton className="h-32 w-full" />
        </Card>
      ) : listQuery.isError ? (
        <Alert tone="error">{describeError(listQuery.error)}</Alert>
      ) : groups.length === 0 ? (
        <EmptyState
          title="No conflicts"
          description="No provider is double-booked in this date range."
        />
      ) : (
        <div className="space-y-4">
          {groups.map((group) => (
            <ConflictGroupCard
              key={`${group.providerId}-${group.slotDate}-${group.windowStart}`}
              group={group}
              canWrite={canWrite}
              onResolved={(message) => {
                setActionError(null);
                setActionNotice(message);
                invalidate();
              }}
              onError={(message) => {
                setActionNotice(null);
                setActionError(message);
              }}
            />
          ))}
        </div>
      )}

      <Pagination
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={totalCount}
        onPageChange={setPage}
      />
    </div>
  );
}

function ConflictGroupCard({
  group,
  canWrite,
  onResolved,
  onError,
}: {
  group: BookingConflictGroup;
  canWrite: boolean;
  onResolved: (message: string) => void;
  onError: (message: string) => void;
}) {
  return (
    <Reveal>
      <Card>
        <div className="flex flex-wrap items-baseline justify-between gap-2 border-b border-line pb-3">
          <div>
            <h3 className="text-base font-semibold text-fg">{group.providerDisplayName}</h3>
            <p className="text-sm text-fg-muted">{group.providerPhone}</p>
          </div>
          <div className="text-right text-sm text-fg-muted">
            <div>{formatDate(group.slotDate)}</div>
            <div className="font-mono">
              {formatSlotTime(group.windowStart)}–{formatSlotTime(group.windowEnd)}
            </div>
          </div>
          <Badge tone="danger">{group.bookings.length} overlapping jobs</Badge>
        </div>

        <div className="divide-y divide-line">
          {group.bookings.map((booking) => (
            <ConflictedBookingRow
              key={booking.bookingId}
              booking={booking}
              canWrite={canWrite}
              onResolved={onResolved}
              onError={onError}
            />
          ))}
        </div>
      </Card>
    </Reveal>
  );
}

function ConflictedBookingRow({
  booking,
  canWrite,
  onResolved,
  onError,
}: {
  booking: ConflictedBooking;
  canWrite: boolean;
  onResolved: (message: string) => void;
  onError: (message: string) => void;
}) {
  const [picking, setPicking] = useState(false);
  const [providerId, setProviderId] = useState("");

  // Only fetched once the admin opens the picker for this row: a group can
  // hold several bookings and each lookup is a server-side match over service
  // area and skill, so loading them all up front would spend that work on
  // rows the admin never touches.
  const eligibleQuery = useQuery({
    queryKey: ["admin-booking-eligible-providers", booking.bookingId],
    queryFn: () => getEligibleProviders(booking.bookingId),
    enabled: picking && canWrite,
  });

  const assignMutation = useMutation({
    mutationFn: () =>
      assignProviderToBooking(booking.bookingId, { providerId }),
    onSuccess: () => {
      setPicking(false);
      setProviderId("");
      onResolved("Booking reassigned — the conflict list has been refreshed.");
    },
    onError: (err) => onError(describeError(err)),
  });

  const accepted = booking.assignmentStatus === AssignmentStatus.Accepted;

  return (
    <div className="py-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <Link
            href={`/bookings/${booking.bookingId}`}
            className="font-medium text-fg underline-offset-2 hover:underline"
          >
            {booking.customerName}
          </Link>
          <p className="text-sm text-fg-muted">
            {booking.serviceName} ·{" "}
            <span className="font-mono">
              {formatSlotTime(booking.startTime)}–{formatSlotTime(booking.endTime)}
            </span>
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {/*
            Accepted vs merely offered is the single most decision-relevant
            fact in this row: moving a job the provider has already committed
            to is a different act from withdrawing one they have not answered.
          */}
          <Badge tone={accepted ? "warning" : "neutral"}>
            {ASSIGNMENT_STATUS_LABELS[booking.assignmentStatus] ?? String(booking.assignmentStatus)}
          </Badge>
          <Badge tone="neutral">{ASSIGNED_BY_LABELS[booking.assignedByType] ?? "—"}</Badge>
          {canWrite ? (
            <Button variant="secondary" onClick={() => setPicking((open) => !open)}>
              {picking ? "Cancel" : "Reassign"}
            </Button>
          ) : null}
        </div>
      </div>

      {picking && canWrite ? (
        <div className="mt-3 flex flex-wrap items-end gap-3 rounded-md bg-surface-2 p-3">
          {eligibleQuery.isPending ? (
            <Skeleton className="h-10 w-64" />
          ) : eligibleQuery.isError ? (
            <Alert tone="error">{describeError(eligibleQuery.error)}</Alert>
          ) : (
            <>
              <Select
                label="Reassign to"
                value={providerId}
                onChange={(e) => setProviderId(e.target.value)}
                options={[
                  { value: "", label: "Select a provider…" },
                  ...(eligibleQuery.data ?? []).map((candidate) => ({
                    value: candidate.providerId,
                    label: `${candidate.displayName} — ${candidate.assignedJobsToday} jobs today${
                      candidate.pincodeMatch ? " · pincode" : ""
                    }${candidate.serviceMatch ? " · service" : ""}`,
                  })),
                ]}
              />
              <Button
                onClick={() => assignMutation.mutate()}
                disabled={!providerId || assignMutation.isPending}
              >
                {assignMutation.isPending ? "Reassigning…" : "Confirm reassignment"}
              </Button>
              {(eligibleQuery.data ?? []).length === 0 ? (
                <p className="text-sm text-fg-muted">
                  No eligible provider matches this booking&apos;s area and service.
                </p>
              ) : null}
            </>
          )}
        </div>
      ) : null}
    </div>
  );
}
