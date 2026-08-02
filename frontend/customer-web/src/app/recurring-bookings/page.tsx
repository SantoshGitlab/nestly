"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import {
  DAY_OF_WEEK_LABELS,
  RecurringBookingPlanStatus,
  RecurringBookingRecurrenceFrequency,
} from "@/lib/types";
import type {
  RecurringBookingPlanResponse,
  UpcomingOccurrenceResponse,
} from "@/lib/types";

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
      apiFetch<RecurringBookingPlanResponse[]>(`${API_V1}/recurring-booking-plans`, { authenticated: true }),
  });

  return (
    <main className="mx-auto w-full max-w-3xl px-6 py-12">
      <PageHeading title="Recurring bookings" subtitle="Manage your standing service schedules." />

      {query.isPending ? (
        <p className="text-sm text-neutral-500">Loading…</p>
      ) : query.isError ? (
        <Alert>{describeError(query.error)}</Alert>
      ) : query.data.length === 0 ? (
        <Card title="No recurring bookings yet">
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            Set one up from a service&apos;s booking summary page to have it repeat automatically.
          </p>
        </Card>
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
  return plan.recurrenceDayOfWeek !== null ? `on ${DAY_OF_WEEK_LABELS[plan.recurrenceDayOfWeek]}s` : "";
}

function PlanCard({ plan }: { plan: RecurringBookingPlanResponse }) {
  const queryClient = useQueryClient();
  const [showUpcoming, setShowUpcoming] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["recurring-booking-plans"] });

  const runAction = useMutation({
    mutationFn: (action: "pause" | "resume" | "cancel") =>
      apiFetch<RecurringBookingPlanResponse>(`${API_V1}/recurring-booking-plans/${plan.id}/${action}`, {
        method: "POST",
        authenticated: true,
      }),
    onSuccess: () => {
      setActionError(null);
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
    plan.status === RecurringBookingPlanStatus.Cancelled || plan.status === RecurringBookingPlanStatus.Completed;

  return (
    <Card title={plan.serviceName}>
      <div className="flex flex-col gap-3 text-sm">
        <div className="flex items-center justify-between">
          <span className="text-neutral-600 dark:text-neutral-400">
            {frequencyLabel(plan.frequency)} {dayDescription(plan)}
          </span>
          <span className="font-medium">{statusLabel(plan.status)}</span>
        </div>

        {!isTerminal ? (
          <div className="flex items-center justify-between">
            <span className="text-neutral-600 dark:text-neutral-400">Next visit</span>
            <span>{plan.nextOccurrenceDate}</span>
          </div>
        ) : null}

        <div className="flex items-center justify-between">
          <span className="text-neutral-600 dark:text-neutral-400">Visits completed</span>
          <span>
            {plan.completedOccurrenceCount}
            {plan.occurrenceCount ? ` of ${plan.occurrenceCount}` : ""}
          </span>
        </div>

        {actionError ? <Alert>{actionError}</Alert> : null}

        <div className="flex flex-wrap gap-2 border-t border-black/10 pt-3 dark:border-white/15">
          {plan.status === RecurringBookingPlanStatus.Active ? (
            <Button
              type="button"
              variant="secondary"
              disabled={runAction.isPending}
              onClick={() => runAction.mutate("pause")}
            >
              Pause
            </Button>
          ) : null}
          {plan.status === RecurringBookingPlanStatus.Paused ? (
            <Button
              type="button"
              variant="secondary"
              disabled={runAction.isPending}
              onClick={() => runAction.mutate("resume")}
            >
              Resume
            </Button>
          ) : null}
          {!isTerminal ? (
            <Button
              type="button"
              variant="danger"
              disabled={runAction.isPending}
              onClick={() => runAction.mutate("cancel")}
            >
              Cancel
            </Button>
          ) : null}
          {!isTerminal ? (
            <Button type="button" variant="secondary" onClick={() => setShowUpcoming((v) => !v)}>
              {showUpcoming ? "Hide upcoming" : "Show upcoming"}
            </Button>
          ) : null}
        </div>

        {showUpcoming ? (
          <div className="border-t border-black/10 pt-3 dark:border-white/15">
            {upcomingQuery.isPending ? (
              <p className="text-neutral-500">Loading…</p>
            ) : upcomingQuery.isError ? (
              <Alert>{describeError(upcomingQuery.error)}</Alert>
            ) : upcomingQuery.data.length === 0 ? (
              <p className="text-neutral-500">No more visits scheduled.</p>
            ) : (
              <ul className="flex flex-col gap-1">
                {upcomingQuery.data.map((occurrence) => (
                  <li key={occurrence.scheduledDate} className="text-neutral-600 dark:text-neutral-400">
                    {occurrence.scheduledDate}
                  </li>
                ))}
              </ul>
            )}
          </div>
        ) : null}
      </div>
    </Card>
  );
}
