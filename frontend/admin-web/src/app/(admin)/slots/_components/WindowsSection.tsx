"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
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

  if (!canWrite) {
    return <span>{window.maxBookingsPerSlot ?? "Unlimited"}</span>;
  }

  if (!isEditing) {
    return (
      <button
        type="button"
        className="underline decoration-dotted underline-offset-2 hover:text-black dark:hover:text-white"
        onClick={() => setIsEditing(true)}
      >
        {window.maxBookingsPerSlot ?? "Unlimited"}
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
        className="w-20 rounded-lg border border-black/15 bg-transparent px-2 py-1 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
      />
      <Button
        type="button"
        className="px-2 py-1 text-xs"
        disabled={mutation.isPending}
        onClick={() => mutation.mutate()}
      >
        {mutation.isPending ? "Saving…" : "Save"}
      </Button>
      <Button
        type="button"
        variant="secondary"
        className="px-2 py-1 text-xs"
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
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["slot-windows"] }),
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

  return (
    <Card title="Slot Windows" description="Recurring time-of-day booking windows and the days of week they run on (SRS 12.10.1).">
      <div className="mb-4 w-64">
        <Select
          label="Filter by city"
          value={cityFilter}
          onChange={(e) => setCityFilter(e.target.value)}
          options={[{ value: "", label: "All cities" }, ...cityOptions]}
        />
      </div>

      {windowsQuery.isLoading ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>
      ) : windowsQuery.error ? (
        <p className="text-sm text-red-600 dark:text-red-400">{describeError(windowsQuery.error)}</p>
      ) : !windowsQuery.data || windowsQuery.data.length === 0 ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">No slot windows yet.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-full text-left text-sm">
            <thead>
              <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
                <th className="px-3 py-2 font-medium">City</th>
                <th className="px-3 py-2 font-medium">Name</th>
                <th className="px-3 py-2 font-medium">Time</th>
                <th className="px-3 py-2 font-medium">Days</th>
                <th className="px-3 py-2 font-medium">Capacity</th>
                <th className="px-3 py-2 font-medium">Status</th>
                {canWrite ? <th className="px-3 py-2 font-medium"><span className="sr-only">Actions</span></th> : null}
              </tr>
            </thead>
            <tbody>
              {windowsQuery.data.map((window) => (
                <tr key={window.id} className="border-b border-black/5 last:border-0 dark:border-white/10">
                  <td className="px-3 py-2">{window.cityName}</td>
                  <td className="px-3 py-2">{window.name}</td>
                  <td className="px-3 py-2 tabular-nums">
                    {toTimeInputValue(window.startTime)}–{toTimeInputValue(window.endTime)}
                  </td>
                  <td className="px-3 py-2">
                    {window.daysOfWeek.length === 0
                      ? <span className="text-neutral-500 dark:text-neutral-400">Not scheduled</span>
                      : window.daysOfWeek.map((d) => DAY_OF_WEEK_LABELS[d]).join(", ")}
                  </td>
                  <td className="px-3 py-2">
                    <CapacityCell window={window} canWrite={canWrite} />
                  </td>
                  <td className="px-3 py-2">
                    <span
                      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                        window.isActive
                          ? "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200"
                          : "bg-neutral-200 text-neutral-700 dark:bg-neutral-800 dark:text-neutral-300"
                      }`}
                    >
                      {window.isActive ? "Active" : "Suspended"}
                    </span>
                  </td>
                  {canWrite ? (
                    <td className="px-3 py-2">
                      <div className="flex gap-2">
                        <Button type="button" variant="secondary" className="px-2 py-1 text-xs" onClick={() => startEditing(window)}>
                          Edit
                        </Button>
                        <Button
                          type="button"
                          variant={window.isActive ? "danger" : "secondary"}
                          className="px-2 py-1 text-xs"
                          disabled={toggleMutation.isPending && toggleMutation.variables?.id === window.id}
                          onClick={() => toggleMutation.mutate({ id: window.id, isActive: !window.isActive })}
                        >
                          {window.isActive ? "Suspend" : "Activate"}
                        </Button>
                      </div>
                    </td>
                  ) : null}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {canWrite ? (
        <form onSubmit={onSubmit} className="mt-5 flex flex-col gap-3 border-t border-black/10 pt-4 dark:border-white/15" noValidate>
          <h3 className="text-sm font-semibold">{editingId ? "Edit window" : "Add a window"}</h3>
          {activeMutationError ? <Alert>{describeError(activeMutationError)}</Alert> : null}
          <div className="flex flex-wrap items-end gap-3">
            <div className="w-48">
              <Select
                label="City"
                placeholder="Select a city…"
                disabled={!!editingId}
                error={form.formState.errors.cityId?.message}
                options={cityOptions}
                {...form.register("cityId")}
              />
            </div>
            <div className="w-40">
              <Field label="Name" error={form.formState.errors.name?.message} {...form.register("name")} />
            </div>
            <div className="w-32">
              <Field label="Start time" type="time" error={form.formState.errors.startTime?.message} {...form.register("startTime")} />
            </div>
            <div className="w-32">
              <Field label="End time" type="time" error={form.formState.errors.endTime?.message} {...form.register("endTime")} />
            </div>
            {!editingId ? (
              <div className="w-36">
                <Field
                  label="Capacity (optional)"
                  type="number"
                  min={1}
                  placeholder="Unlimited"
                  {...form.register("maxBookingsPerSlot")}
                />
              </div>
            ) : null}
          </div>

          <div>
            <p className="mb-1.5 text-sm font-medium">Days of week</p>
            <div className="flex flex-wrap gap-2">
              {ALL_DAYS_OF_WEEK.map((day) => (
                <label
                  key={day}
                  className={`cursor-pointer rounded-lg border px-3 py-1.5 text-sm transition-colors ${
                    daysOfWeek.includes(day)
                      ? "border-black bg-black text-white dark:border-white dark:bg-white dark:text-black"
                      : "border-black/15 dark:border-white/20"
                  }`}
                >
                  <input type="checkbox" className="sr-only" checked={daysOfWeek.includes(day)} onChange={() => toggleDay(day)} />
                  {DAY_OF_WEEK_LABELS[day]}
                </label>
              ))}
            </div>
          </div>

          <div className="flex gap-2">
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Saving…" : editingId ? "Save changes" : "Add window"}
            </Button>
            {editingId ? (
              <Button type="button" variant="secondary" onClick={cancelEditing}>
                Cancel
              </Button>
            ) : null}
          </div>
        </form>
      ) : null}
    </Card>
  );
}
