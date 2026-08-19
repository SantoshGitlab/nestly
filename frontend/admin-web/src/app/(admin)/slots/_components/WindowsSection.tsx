"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select, cx } from "@/components/ui";
import { ActiveBadge, ConfirmDialog, DataTable, FormActions, FormGrid } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { describeError } from "@/lib/api";
import {
  createSlotWindow,
  listSlotCityLookups,
  listSlotWindows,
  setSlotWindowActive,
  setSlotWindowCapacity,
  updateSlotWindow,
} from "@/lib/slots-api";
import { ALL_DAYS_OF_WEEK, DAY_OF_WEEK_LABELS, DayOfWeek } from "@/lib/slots-types";
import type { SlotWindowAdminResponse } from "@/lib/slots-types";

/** "09:00:00" (TimeSpan over the wire) <-> "09:00" (<input type="time"> value). */
const toTimeInputValue = (timeSpan: string) => timeSpan.slice(0, 5);
const toApiTimeValue = (inputValue: string) => (inputValue.length === 5 ? `${inputValue}:00` : inputValue);

const windowSchema = z.object({
  cityId: z.string().min(1, "Select a city"),
  name: z.string().min(1, "Window name is required").max(100),
  startTime: z.string().min(1, "Start time is required"),
  endTime: z.string().min(1, "End time is required"),
  maxBookingsPerSlot: z.string(),
  daysOfWeek: z.array(z.number()),
});
type WindowFormValues = z.infer<typeof windowSchema>;

const emptyFormValues: WindowFormValues = {
  cityId: "",
  name: "",
  startTime: "09:00",
  endTime: "13:00",
  maxBookingsPerSlot: "",
  daysOfWeek: [],
};

/** Per-row capacity editor (task 113d) - its own control since capacity has its own dedicated PATCH endpoint. */
function CapacityCell({ window, canWrite }: { window: SlotWindowAdminResponse; canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [value, setValue] = useState(window.maxBookingsPerSlot?.toString() ?? "");
  const [isEditing, setIsEditing] = useState(false);

  const mutation = useMutation({
    mutationFn: () =>
      setSlotWindowCapacity(window.id, { maxBookingsPerSlot: value.trim() === "" ? null : Number(value) }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["slot-windows"] });
      setIsEditing(false);
    },
  });

  const display = window.maxBookingsPerSlot ?? "Unlimited";

  if (!canWrite) {
    return <span className="nums">{display}</span>;
  }

  if (!isEditing) {
    return (
      <button
        type="button"
        className="nums rounded underline decoration-dotted underline-offset-4 transition-colors duration-fast ease-out hover:text-brand-600 dark:hover:text-brand-400"
        onClick={() => setIsEditing(true)}
      >
        {display}
      </button>
    );
  }

  return (
    <div className="flex items-center gap-1.5">
      <input
        type="number"
        min={1}
        value={value}
        onChange={(e) => setValue(e.target.value)}
        placeholder="Unlimited"
        aria-label={`Capacity for ${window.name}`}
        autoFocus
        className="nums w-24 rounded-lg border border-line bg-surface px-2 py-1 text-sm text-fg shadow-xs outline-none transition duration-fast ease-out placeholder:text-fg-subtle hover:border-line-strong focus:border-brand-600 focus:ring-2 focus:ring-brand-600/25"
      />
      <Button type="button" size="sm" loading={mutation.isPending} onClick={() => mutation.mutate()}>
        Save
      </Button>
      <Button
        type="button"
        size="sm"
        variant="ghost"
        onClick={() => {
          setValue(window.maxBookingsPerSlot?.toString() ?? "");
          setIsEditing(false);
        }}
      >
        Cancel
      </Button>
    </div>
  );
}

/** Slot windows: recurring time-of-day booking windows and their day-of-week rules (SRS 12.10.1, task 113a). Capacity is task 113d. */
export function WindowsSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [cityFilter, setCityFilter] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [pendingSuspend, setPendingSuspend] = useState<SlotWindowAdminResponse | null>(null);

  const citiesQuery = useQuery({ queryKey: ["slot-cities"], queryFn: listSlotCityLookups });
  const windowsQuery = useQuery({
    queryKey: ["slot-windows", cityFilter],
    queryFn: () => listSlotWindows(cityFilter || undefined),
  });

  const form = useForm<WindowFormValues>({
    resolver: zodResolver(windowSchema),
    defaultValues: emptyFormValues,
  });

  const createMutation = useMutation({
    mutationFn: (values: WindowFormValues) =>
      createSlotWindow({
        cityId: values.cityId,
        name: values.name,
        startTime: toApiTimeValue(values.startTime),
        endTime: toApiTimeValue(values.endTime),
        maxBookingsPerSlot: values.maxBookingsPerSlot.trim() === "" ? null : Number(values.maxBookingsPerSlot),
        daysOfWeek: values.daysOfWeek,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["slot-windows"] });
      form.reset({ ...emptyFormValues, cityId: form.getValues("cityId") });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, values }: { id: string; values: WindowFormValues }) =>
      updateSlotWindow(id, {
        name: values.name,
        startTime: toApiTimeValue(values.startTime),
        endTime: toApiTimeValue(values.endTime),
        daysOfWeek: values.daysOfWeek,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["slot-windows"] });
      setEditingId(null);
      form.reset(emptyFormValues);
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setSlotWindowActive(id, isActive),
    onSuccess: () => {
      setPendingSuspend(null);
      queryClient.invalidateQueries({ queryKey: ["slot-windows"] });
    },
  });

  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const daysOfWeek = form.watch("daysOfWeek");

  function toggleDay(day: DayOfWeek) {
    const current = form.getValues("daysOfWeek");
    form.setValue(
      "daysOfWeek",
      current.includes(day) ? current.filter((d) => d !== day) : [...current, day],
    );
  }

  function startEditing(window: SlotWindowAdminResponse) {
    setEditingId(window.id);
    form.reset({
      cityId: window.cityId,
      name: window.name,
      startTime: toTimeInputValue(window.startTime),
      endTime: toTimeInputValue(window.endTime),
      maxBookingsPerSlot: window.maxBookingsPerSlot?.toString() ?? "",
      daysOfWeek: window.daysOfWeek,
    });
  }

  function cancelEditing() {
    setEditingId(null);
    form.reset(emptyFormValues);
  }

  const onSubmit = form.handleSubmit((values) => {
    if (editingId) {
      updateMutation.mutate({ id: editingId, values });
    } else {
      createMutation.mutate(values);
    }
  });

  const activeMutationError = editingId ? updateMutation.error : createMutation.error;
  const isSubmitting = editingId ? updateMutation.isPending : createMutation.isPending;

  const columns: DataTableColumn<SlotWindowAdminResponse>[] = [
    { key: "city", header: "City", sortValue: (w) => w.cityName, cell: (w) => w.cityName },
    {
      key: "name",
      header: "Name",
      sortValue: (w) => w.name,
      cell: (w) => <span className="font-medium text-fg">{w.name}</span>,
    },
    {
      key: "time",
      header: "Time",
      sortValue: (w) => w.startTime,
      cell: (w) => (
        <span className="nums whitespace-nowrap">
          {toTimeInputValue(w.startTime)}–{toTimeInputValue(w.endTime)}
        </span>
      ),
    },
    {
      key: "days",
      header: "Days",
      cell: (w) =>
        w.daysOfWeek.length === 0 ? (
          <span className="text-fg-subtle">Not scheduled</span>
        ) : (
          w.daysOfWeek.map((d) => DAY_OF_WEEK_LABELS[d]).join(", ")
        ),
    },
    {
      key: "capacity",
      header: "Capacity",
      cell: (w) => <CapacityCell window={w} canWrite={canWrite} />,
    },
    {
      key: "status",
      header: "Status",
      sortValue: (w) => w.isActive,
      cell: (w) => <ActiveBadge active={w.isActive} inactiveLabel="Suspended" />,
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <DataTable
        title="Slot windows"
        description="Recurring time-of-day booking windows and the days of week they run on (SRS 12.10.1)."
        actions={
          <div className="w-56">
            <Select
              label="Filter by city"
              value={cityFilter}
              onChange={(e) => setCityFilter(e.target.value)}
              options={[{ value: "", label: "All cities" }, ...cityOptions]}
            />
          </div>
        }
        columns={columns}
        rows={windowsQuery.data}
        rowKey={(w) => w.id}
        isLoading={windowsQuery.isPending}
        isFetching={windowsQuery.isFetching}
        error={windowsQuery.error}
        onRetry={() => windowsQuery.refetch()}
        caption="Slot windows"
        emptyTitle={cityFilter ? "No slot windows in this city" : "No slot windows yet"}
        emptyDescription="Without a window, nothing in this city is bookable."
        emptyAction={
          cityFilter ? (
            <Button variant="secondary" onClick={() => setCityFilter("")}>
              Show all cities
            </Button>
          ) : undefined
        }
        skeletonRows={5}
        minWidth="1000px"
        maxHeight="420px"
        rowActions={
          canWrite
            ? (w) => (
                <>
                  <Button type="button" size="sm" variant="ghost" onClick={() => startEditing(w)}>
                    Edit
                  </Button>
                  {w.isActive ? (
                    <Button type="button" size="sm" variant="secondary" onClick={() => setPendingSuspend(w)}>
                      Suspend
                    </Button>
                  ) : (
                    <Button
                      type="button"
                      size="sm"
                      variant="subtle"
                      loading={toggleMutation.isPending && toggleMutation.variables?.id === w.id}
                      onClick={() => toggleMutation.mutate({ id: w.id, isActive: true })}
                    >
                      Activate
                    </Button>
                  )}
                </>
              )
            : undefined
        }
      />

      {canWrite ? (
        <Card
          title={editingId ? "Edit window" : "Add a window"}
          description={
            editingId
              ? "The city cannot be changed on an existing window — create a new one instead."
              : "A window is only bookable on the days of week you select below."
          }
        >
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {activeMutationError ? <Alert>{describeError(activeMutationError)}</Alert> : null}

            <FormGrid columns={3}>
              {/* Explicit id: this page renders four sections at once (Windows,
                  Blackouts, Cutoffs, Overrides), each with its own "cityId"-named
                  field - without an id, Field/controlId's `field-${name}` fallback
                  collides across all four, duplicating `id="field-cityId"` on the
                  same page and breaking label/input association. */}
              <Select
                id="window-cityId"
                label="City"
                required
                placeholder="Select a city…"
                disabled={!!editingId}
                error={form.formState.errors.cityId?.message}
                options={cityOptions}
                {...form.register("cityId")}
              />
              <Field label="Name" required error={form.formState.errors.name?.message} {...form.register("name")} />
              {!editingId ? (
                <Field
                  label="Capacity"
                  type="number"
                  min={1}
                  placeholder="Unlimited"
                  hint="Leave empty for unlimited bookings per slot."
                  {...form.register("maxBookingsPerSlot")}
                />
              ) : null}
              <Field label="Start time" type="time" required error={form.formState.errors.startTime?.message} {...form.register("startTime")} />
              <Field label="End time" type="time" required error={form.formState.errors.endTime?.message} {...form.register("endTime")} />
            </FormGrid>

            <fieldset>
              <legend className="mb-2 text-sm font-medium text-fg">Days of week</legend>
              <div className="flex flex-wrap gap-2">
                {ALL_DAYS_OF_WEEK.map((day) => {
                  const selected = daysOfWeek.includes(day);
                  return (
                    <label
                      key={day}
                      className={cx(
                        "cursor-pointer select-none rounded-lg border px-3 py-1.5 text-sm font-medium transition duration-fast ease-out",
                        selected
                          ? "border-brand-600 bg-brand-600 text-fg-on-brand shadow-brand"
                          : "border-line bg-surface text-fg-muted hover:border-line-strong hover:bg-surface-2 hover:text-fg",
                      )}
                    >
                      <input
                        type="checkbox"
                        className="sr-only"
                        checked={selected}
                        onChange={() => toggleDay(day)}
                      />
                      {DAY_OF_WEEK_LABELS[day]}
                    </label>
                  );
                })}
              </div>
            </fieldset>

            <FormActions align="start">
              <Button type="submit" loading={isSubmitting}>
                {editingId ? "Save changes" : "Add window"}
              </Button>
              {editingId ? (
                <Button type="button" variant="secondary" onClick={cancelEditing}>
                  Cancel
                </Button>
              ) : null}
            </FormActions>
          </form>
        </Card>
      ) : null}

      <ConfirmDialog
        open={pendingSuspend !== null}
        title="Suspend this slot window?"
        description="Customers stop being offered it immediately. Bookings already made in it are unaffected."
        confirmLabel="Suspend"
        cancelLabel="Keep active"
        loading={toggleMutation.isPending}
        error={toggleMutation.isError ? describeError(toggleMutation.error) : null}
        onCancel={() => setPendingSuspend(null)}
        onConfirm={() => {
          if (pendingSuspend) toggleMutation.mutate({ id: pendingSuspend.id, isActive: false });
        }}
      >
        {pendingSuspend ? (
          <p className="text-sm text-fg-muted">
            <span className="font-medium text-fg">{pendingSuspend.name}</span> in {pendingSuspend.cityName}.
          </p>
        ) : null}
      </ConfirmDialog>
    </div>
  );
}
