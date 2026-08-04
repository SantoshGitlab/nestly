"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import {
  DetailList,
  DetailRow,
  formatCalendarDate,
  recurringPlanStatusTone,
} from "@/components/patterns";
import { RequireAuth } from "@/components/RequireAuth";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Modal,
  PageHeading,
  Skeleton,
} from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import {
  DAY_OF_WEEK_LABELS,
  RecurringBookingPlanStatus,
  RecurringBookingRecurrenceFrequency,
} from "@/lib/types";
import type { RecurringBookingPlanResponse, UpcomingOccurrenceResponse } from "@/lib/types";

/**
 * Manage recurring booking plans (task 187): pause/resume/cancel and a
 * projected-upcoming-dates preview per plan. The create flow lives at
 * /recurring-bookings/new, reached from the booking summary and booking
 * detail pages.
 */
export default function RecurringBookingsPage() {
  return (
    <RequireAuth>
      <RecurringBookingsScreen />
    </RequireAuth>
  );
}

function RecurringBookingsScreen() {
  const query = useQuery({
    queryKey: ["recurring-booking-plans"],
    queryFn: () =>
      apiFetch<RecurringBookingPlanResponse[]>(`${API_V1}/recurring-booking-plans`, {
        authenticated: true,
      }),
  });

  return (
    <main className="mx-auto w-full max-w-3xl px-4 py-8 sm:px-6 sm:py-12">
      <PageHeading
        title="Recurring bookings"
        subtitle="Manage your standing service schedules."
      />

      {query.isPending ? (
        <ul className="flex flex-col gap-4" aria-hidden>
          {[0, 1].map((row) => (
            <li key={row}>
              <Skeleton className="h-56 rounded-2xl" />
            </li>
          ))}
        </ul>
      ) : query.isError ? (
        <Alert
          tone="error"
          title="Couldn't load your recurring plans"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      ) : query.data.length === 0 ? (
        <EmptyState
          title="No recurring bookings yet"
          description="Set one up from any service's booking summary and it will repeat on the schedule you choose."
          action={
            <Link
              href="/categories"
              className="inline-flex h-10 items-center justify-center rounded-lg bg-brand-600 px-4 text-sm font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700"
            >
              Browse services
            </Link>
          }
        />
      ) : (
        <ul className="flex flex-col gap-4">
          {query.data.map((plan) => (
            <li key={plan.id}>
              <PlanCard plan={plan} />
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}

function frequencyLabel(frequency: RecurringBookingRecurrenceFrequency): string {
  switch (frequency) {
    case RecurringBookingRecurrenceFrequency.Weekly:
      return "Every week";
    case RecurringBookingRecurrenceFrequency.Biweekly:
      return "Every 2 weeks";
    case RecurringBookingRecurrenceFrequency.Monthly:
      return "Every month";
    default:
      return "Unknown";
  }
}

function statusLabel(status: RecurringBookingPlanStatus): string {
  switch (status) {
    case RecurringBookingPlanStatus.Active:
      return "Active";
    case RecurringBookingPlanStatus.Paused:
      return "Paused";
    case RecurringBookingPlanStatus.Cancelled:
      return "Cancelled";
    case RecurringBookingPlanStatus.Completed:
      return "Completed";
    default:
      return "Unknown";
  }
}

function dayDescription(plan: RecurringBookingPlanResponse): string {
  if (plan.frequency === RecurringBookingRecurrenceFrequency.Monthly) {
    return `on day ${plan.recurrenceDayOfMonth}`;
  }
  return plan.recurrenceDayOfWeek !== null
    ? `on ${DAY_OF_WEEK_LABELS[plan.recurrenceDayOfWeek]}s`
    : "";
}

function PlanCard({ plan }: { plan: RecurringBookingPlanResponse }) {
  const queryClient = useQueryClient();
  const [showUpcoming, setShowUpcoming] = useState(false);
  const [confirmingCancel, setConfirmingCancel] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["recurring-booking-plans"] });

  const runAction = useMutation({
    mutationFn: (action: "pause" | "resume" | "cancel") =>
      apiFetch<RecurringBookingPlanResponse>(
        `${API_V1}/recurring-booking-plans/${plan.id}/${action}`,
        { method: "POST", authenticated: true },
      ),
    onSuccess: () => {
      setActionError(null);
      setConfirmingCancel(false);
      invalidate();
    },
    onError: (err) => setActionError(describeError(err)),
  });

  const upcomingQuery = useQuery({
    queryKey: ["recurring-booking-plan-upcoming", plan.id],
    queryFn: () =>
      apiFetch<UpcomingOccurrenceResponse[]>(
        `${API_V1}/recurring-booking-plans/${plan.id}/occurrences/upcoming?count=5`,
        { authenticated: true },
      ),
    enabled: showUpcoming,
  });

  const isTerminal =
    plan.status === RecurringBookingPlanStatus.Cancelled ||
    plan.status === RecurringBookingPlanStatus.Completed;

  const progress =
    plan.occurrenceCount && plan.occurrenceCount > 0
      ? Math.min(100, Math.round((plan.completedOccurrenceCount / plan.occurrenceCount) * 100))
      : null;

  return (
    <Card
      title={plan.serviceName}
      description={`${frequencyLabel(plan.frequency)} ${dayDescription(plan)}`.trim()}
      actions={
        <Badge tone={recurringPlanStatusTone(plan.status)}>{statusLabel(plan.status)}</Badge>
      }
    >
      <div className="flex flex-col gap-4">
        <DetailList>
          {!isTerminal ? (
            <DetailRow label="Next visit">{formatCalendarDate(plan.nextOccurrenceDate)}</DetailRow>
          ) : null}
          <DetailRow label="Visits completed" numeric>
            {plan.completedOccurrenceCount}
            {plan.occurrenceCount ? ` of ${plan.occurrenceCount}` : ""}
          </DetailRow>
          <DetailRow label="Started">{formatCalendarDate(plan.startDate)}</DetailRow>
          {plan.endDate ? (
            <DetailRow label="Ends">{formatCalendarDate(plan.endDate)}</DetailRow>
          ) : null}
        </DetailList>

        {progress !== null ? (
          <div>
            <div
              className="h-1.5 w-full overflow-hidden rounded-full bg-surface-3"
              role="progressbar"
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={progress}
              aria-label={`${plan.serviceName} plan progress`}
            >
              <div
                className="h-full rounded-full bg-brand-600 transition-[width] duration-slow ease-out"
                style={{ width: `${progress}%` }}
              />
            </div>
            <p className="nums mt-1.5 text-xs text-fg-subtle">{progress}% complete</p>
          </div>
        ) : null}

        {actionError ? (
          <Alert tone="error" title="That didn't work">
            {actionError}
          </Alert>
        ) : null}

        <div className="flex flex-wrap gap-2 border-t border-line pt-4">
          {plan.status === RecurringBookingPlanStatus.Active ? (
            <Button
              type="button"
              variant="secondary"
              loading={runAction.isPending && runAction.variables === "pause"}
              onClick={() => runAction.mutate("pause")}
            >
              Pause
            </Button>
          ) : null}
          {plan.status === RecurringBookingPlanStatus.Paused ? (
            <Button
              type="button"
              variant="secondary"
              loading={runAction.isPending && runAction.variables === "resume"}
              onClick={() => runAction.mutate("resume")}
            >
              Resume
            </Button>
          ) : null}
          {!isTerminal ? (
            <Button type="button" variant="secondary" onClick={() => setShowUpcoming((v) => !v)}>
              {showUpcoming ? "Hide upcoming" : "Show upcoming"}
            </Button>
          ) : null}
          {!isTerminal ? (
            <Button
              type="button"
              variant="ghost"
              className="text-danger hover:bg-danger-soft hover:text-danger"
              onClick={() => setConfirmingCancel(true)}
            >
              Cancel plan
            </Button>
          ) : null}
        </div>

        {showUpcoming ? (
          <div className="border-t border-line pt-4">
            <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-fg-subtle">
              Next visits
            </p>
            {upcomingQuery.isPending ? (
              <div className="flex flex-col gap-2" aria-hidden>
                <Skeleton className="h-3.5 w-40" />
                <Skeleton className="h-3.5 w-36" />
              </div>
            ) : upcomingQuery.isError ? (
              <Alert
                tone="error"
                action={
                  <Button size="sm" variant="secondary" onClick={() => upcomingQuery.refetch()}>
                    Retry
                  </Button>
                }
              >
                {describeError(upcomingQuery.error)}
              </Alert>
            ) : upcomingQuery.data.length === 0 ? (
              <p className="text-sm text-fg-muted">No more visits scheduled.</p>
            ) : (
              <ul className="flex flex-col gap-1.5">
                {upcomingQuery.data.map((occurrence) => (
                  <li
                    key={occurrence.scheduledDate}
                    className="flex items-center gap-2 text-sm text-fg-muted"
                  >
                    <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-brand-600" />
                    <span className="nums">{formatCalendarDate(occurrence.scheduledDate)}</span>
                    {occurrence.isProjected ? (
                      <span className="text-xs text-fg-subtle">(projected)</span>
                    ) : null}
                  </li>
                ))}
              </ul>
            )}
          </div>
        ) : null}
      </div>

      {/* Cancelling a plan stops every future visit at once, so it gets an
          explicit confirmation step rather than firing on the first click. */}
      <Modal
        open={confirmingCancel}
        onClose={() => setConfirmingCancel(false)}
        title="Cancel this recurring plan?"
        description={`No further ${plan.serviceName} visits will be booked. Visits already booked stay in place — cancel those individually if you need to.`}
        size="sm"
        footer={
          <>
            <Button type="button" variant="secondary" onClick={() => setConfirmingCancel(false)}>
              Keep plan
            </Button>
            <Button
              type="button"
              variant="danger"
              loading={runAction.isPending && runAction.variables === "cancel"}
              onClick={() => runAction.mutate("cancel")}
            >
              Yes, cancel plan
            </Button>
          </>
        }
      >
        <p className="text-sm leading-relaxed text-fg-muted">
          This can&apos;t be undone — you&apos;d need to set up a new plan to resume the schedule.
        </p>
      </Modal>
    </Card>
  );
}
