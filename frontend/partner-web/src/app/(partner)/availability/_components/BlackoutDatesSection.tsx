"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ErrorState } from "@/components/states";
import {
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  IconButton,
  Modal,
  Skeleton,
  cx,
  useToast,
} from "@/components/ui";
import { createBlackoutDate, deleteBlackoutDate, listBlackoutDates } from "@/lib/availability-api";
import { isoDateOffsetFromToday, todayIsoDate } from "@/lib/date";
import { formatIsoDate } from "@/lib/format";
import type { BlackoutDate } from "@/lib/availability-types";

const blackoutSchema = z
  .object({
    startDate: z.string().min(1, "Start date is required"),
    endDate: z.string().min(1, "End date is required"),
    reason: z.string().max(500),
  })
  .refine((values) => values.endDate >= values.startDate, {
    message: "The end date can't be before the start date.",
    path: ["endDate"],
  });
type BlackoutFormValues = z.infer<typeof blackoutSchema>;

const EMPTY_FORM: BlackoutFormValues = { startDate: "", endDate: "", reason: "" };

/**
 * Ad hoc dates a partner is not available on, overriding the weekly windows
 * above (docs/PARTNER.md's `partner_availability` blackout dates).
 *
 * Every date here is a *calendar day*, so all of it goes through lib/date.ts.
 * `new Date().toISOString().slice(0, 10)` - the obvious way to get "today" -
 * converts to UTC first and returns yesterday in IST before 05:30, which on
 * this screen would mean a partner tapping "Today" blocking the wrong day.
 *
 * Removing a blackout is a one-tap destructive action on a phone, so it asks
 * once: the cost of a mis-tap is being assigned work on a day you are not
 * available, and there is no undo.
 */
export function BlackoutDatesSection() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [pendingRemoval, setPendingRemoval] = useState<BlackoutDate | null>(null);

  const query = useQuery({ queryKey: ["partner-blackout-dates"], queryFn: listBlackoutDates });

  const form = useForm<BlackoutFormValues>({
    resolver: zodResolver(blackoutSchema),
    defaultValues: EMPTY_FORM,
  });

  const createMutation = useMutation({
    mutationFn: (values: BlackoutFormValues) =>
      createBlackoutDate({
        startDate: values.startDate,
        endDate: values.endDate,
        reason: values.reason.trim() === "" ? undefined : values.reason,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["partner-blackout-dates"] });
      form.reset(EMPTY_FORM);
      toast("success", "Blackout date added.");
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteBlackoutDate,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["partner-blackout-dates"] });
      setPendingRemoval(null);
      toast("info", "Blackout date removed.");
    },
  });

  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  const startDateField = form.register("startDate");
  const today = todayIsoDate();

  /** Sets both ends of the range at once, for the single-day quick picks. */
  function setSingleDay(isoDate: string) {
    form.setValue("startDate", isoDate, { shouldValidate: true });
    form.setValue("endDate", isoDate, { shouldValidate: true });
  }

  return (
    <Card
      title="Blackout dates"
      description="Days you're unavailable, even inside your normal weekly hours."
    >
      <div className="flex flex-col gap-5">
        {query.isPending ? (
          <div className="flex flex-col gap-2.5" aria-hidden>
            {Array.from({ length: 2 }, (_, index) => (
              <Skeleton key={index} className="h-16 w-full rounded-xl" />
            ))}
          </div>
        ) : query.isError ? (
          <ErrorState
            title="Couldn't load your blackout dates"
            error={query.error}
            onRetry={() => query.refetch()}
            isRetrying={query.isRefetching}
          />
        ) : query.data.length === 0 ? (
          <EmptyState
            title="No blackout dates"
            description="Away for a few days, or busy on a particular date? Block it here and you won't be assigned jobs on it."
            action={
              <Button variant="secondary" onClick={() => form.setFocus("startDate")}>
                Block a date
              </Button>
            }
          />
        ) : (
          <ul className="flex flex-col gap-2.5">
            {sortForDisplay(query.data, today).map((blackout) => (
              <li key={blackout.id}>
                <BlackoutRow
                  blackout={blackout}
                  isPast={blackout.endDate < today}
                  onRemove={() => setPendingRemoval(blackout)}
                />
              </li>
            ))}
          </ul>
        )}

        <form onSubmit={onSubmit} className="flex flex-col gap-4 border-t border-line pt-5" noValidate>
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="mr-auto text-sm font-semibold text-fg">Block a date</h3>
            <Button type="button" variant="ghost" size="sm" onClick={() => setSingleDay(today)}>
              Today
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setSingleDay(isoDateOffsetFromToday(1))}
            >
              Tomorrow
            </Button>
          </div>

          {createMutation.isError ? (
            <ErrorState title="Couldn't add that blackout date" error={createMutation.error} />
          ) : null}

          <div className="grid gap-3 sm:grid-cols-2">
            <Field
              label="From"
              type="date"
              min={today}
              className="nums"
              error={form.formState.errors.startDate?.message}
              {...startDateField}
              onChange={(event) => {
                startDateField.onChange(event);
                // Most blackouts are a single day; keeping the end in step
                // removes a second date-picker interaction on a phone, and
                // stops the range being invalid the moment it is typed.
                const start = event.target.value;
                const end = form.getValues("endDate");
                if (start && (!end || end < start)) {
                  form.setValue("endDate", start, { shouldValidate: true });
                }
              }}
            />
            <Field
              label="To"
              type="date"
              min={form.watch("startDate") || today}
              className="nums"
              error={form.formState.errors.endDate?.message}
              {...form.register("endDate")}
            />
          </div>

          <Field
            label="Reason (optional)"
            placeholder="Travelling, family event…"
            error={form.formState.errors.reason?.message}
            {...form.register("reason")}
          />

          <Button type="submit" size="lg" fullWidth loading={createMutation.isPending}>
            Add blackout date
          </Button>
        </form>
      </div>

      <Modal
        open={pendingRemoval !== null}
        onClose={() => setPendingRemoval(null)}
        title="Remove this blackout?"
        description="You'll be available for jobs on these dates again."
        size="sm"
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => setPendingRemoval(null)}
              disabled={deleteMutation.isPending}
            >
              Keep it
            </Button>
            <Button
              variant="danger"
              loading={deleteMutation.isPending}
              onClick={() => {
                if (pendingRemoval) deleteMutation.mutate(pendingRemoval.id);
              }}
            >
              Yes, remove
            </Button>
          </>
        }
      >
        {pendingRemoval ? (
          <div className="flex flex-col gap-2 text-sm">
            <p className="nums font-medium text-fg">{rangeLabel(pendingRemoval)}</p>
            {pendingRemoval.reason ? (
              <p className="text-fg-muted">{pendingRemoval.reason}</p>
            ) : null}
            {deleteMutation.isError ? (
              <ErrorState title="Couldn't remove that" error={deleteMutation.error} />
            ) : null}
          </div>
        ) : null}
      </Modal>
    </Card>
  );
}

/** One blackout period. Past ones stay visible but recede. */
function BlackoutRow({
  blackout,
  isPast,
  onRemove,
}: {
  blackout: BlackoutDate;
  isPast: boolean;
  onRemove: () => void;
}) {
  return (
    <div
      className={cx(
        "flex items-start justify-between gap-3 rounded-xl border border-line p-3.5",
        isPast ? "bg-surface" : "bg-surface-2",
      )}
    >
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <p className={cx("nums text-sm font-medium", isPast ? "text-fg-subtle" : "text-fg")}>
            {rangeLabel(blackout)}
          </p>
          {isPast ? <Badge tone="neutral">Past</Badge> : null}
        </div>
        {blackout.reason ? (
          <p className="mt-0.5 text-sm leading-relaxed text-fg-muted">{blackout.reason}</p>
        ) : null}
      </div>

      <IconButton
        label={`Remove blackout ${rangeLabel(blackout)}`}
        onClick={onRemove}
        className="-mr-1.5 -mt-1.5 shrink-0"
      >
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          className="h-4 w-4"
          aria-hidden
        >
          <path d="M18 6 6 18M6 6l12 12" />
        </svg>
      </IconButton>
    </div>
  );
}

/** "3 Aug 2026" for a single day, "3 Aug 2026 – 7 Aug 2026" for a range. */
function rangeLabel(blackout: BlackoutDate): string {
  return blackout.startDate === blackout.endDate
    ? formatIsoDate(blackout.startDate)
    : `${formatIsoDate(blackout.startDate)} – ${formatIsoDate(blackout.endDate)}`;
}

/**
 * Upcoming blackouts first (soonest at the top, since that is the one a
 * partner is checking), then the finished ones newest-first below. Display
 * order only - the API returns no guaranteed ordering.
 */
function sortForDisplay(blackouts: BlackoutDate[], today: string): BlackoutDate[] {
  const upcoming = blackouts
    .filter((blackout) => blackout.endDate >= today)
    .sort((a, b) => (a.startDate < b.startDate ? -1 : 1));
  const past = blackouts
    .filter((blackout) => blackout.endDate < today)
    .sort((a, b) => (a.startDate > b.startDate ? -1 : 1));
  return [...upcoming, ...past];
}
