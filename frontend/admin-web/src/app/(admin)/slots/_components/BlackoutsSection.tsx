"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Badge, Button, Card, Field, Select } from "@/components/ui";
import { ConfirmDialog, DataTable, FormActions, FormGrid } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { describeError } from "@/lib/api";
import { createSlotBlackout, deleteSlotBlackout, listSlotBlackouts, listSlotCityLookups } from "@/lib/slots-api";
import { SLOT_BLACKOUT_TYPE_LABELS, SlotBlackoutType } from "@/lib/slots-types";
import type { SlotBlackoutAdminResponse } from "@/lib/slots-types";

const blackoutSchema = z
  .object({
    cityId: z.string().min(1, "Select a city"),
    startDate: z.string().min(1, "Start date is required"),
    endDate: z.string().min(1, "End date is required"),
    type: z.string().refine((v) => v === String(SlotBlackoutType.Holiday) || v === String(SlotBlackoutType.Blackout), "Select a type"),
    reason: z.string().max(500, "Keep the reason under 500 characters"),
  })
  .refine((values) => values.endDate >= values.startDate, {
    message: "The end date must not be before the start date.",
    path: ["endDate"],
  });
type BlackoutFormValues = z.infer<typeof blackoutSchema>;

const emptyFormValues: BlackoutFormValues = {
  cityId: "",
  startDate: "",
  endDate: "",
  type: String(SlotBlackoutType.Holiday),
  reason: "",
};

/** Holiday and ad hoc blackout dates a city is not bookable on (SRS 12.10.1 "Holiday blackout", task 113b). */
export function BlackoutsSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [cityFilter, setCityFilter] = useState("");
  const [pendingDelete, setPendingDelete] = useState<SlotBlackoutAdminResponse | null>(null);

  const citiesQuery = useQuery({ queryKey: ["slot-cities"], queryFn: listSlotCityLookups });
  const blackoutsQuery = useQuery({
    queryKey: ["slot-blackouts", cityFilter],
    queryFn: () => listSlotBlackouts(cityFilter || undefined),
  });

  const form = useForm<BlackoutFormValues>({
    resolver: zodResolver(blackoutSchema),
    defaultValues: emptyFormValues,
  });

  const createMutation = useMutation({
    mutationFn: (values: BlackoutFormValues) =>
      createSlotBlackout({
        cityId: values.cityId,
        startDate: values.startDate,
        endDate: values.endDate,
        type: Number(values.type) as SlotBlackoutType,
        reason: values.reason.trim() === "" ? null : values.reason.trim(),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["slot-blackouts"] });
      // Keep the city so an admin entering a run of holidays is not re-picking it every time.
      form.reset({ ...emptyFormValues, cityId: form.getValues("cityId") });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteSlotBlackout,
    onSuccess: () => {
      setPendingDelete(null);
      queryClient.invalidateQueries({ queryKey: ["slot-blackouts"] });
    },
  });

  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const typeOptions = [
    { value: String(SlotBlackoutType.Holiday), label: SLOT_BLACKOUT_TYPE_LABELS[SlotBlackoutType.Holiday] },
    { value: String(SlotBlackoutType.Blackout), label: SLOT_BLACKOUT_TYPE_LABELS[SlotBlackoutType.Blackout] },
  ];

  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  const columns: DataTableColumn<SlotBlackoutAdminResponse>[] = [
    { key: "city", header: "City", sortValue: (b) => b.cityName, cell: (b) => b.cityName },
    {
      key: "dates",
      header: "Dates",
      // The endpoint promises no ordering, so sort explicitly rather than
      // trusting the response order to be chronological.
      sortValue: (b) => b.startDate,
      cell: (b) => (
        <span className="nums whitespace-nowrap">
          {b.startDate === b.endDate ? b.startDate : `${b.startDate} – ${b.endDate}`}
        </span>
      ),
    },
    {
      key: "type",
      header: "Type",
      sortValue: (b) => SLOT_BLACKOUT_TYPE_LABELS[b.type],
      cell: (b) => (
        <Badge tone={b.type === SlotBlackoutType.Holiday ? "info" : "warning"}>
          {SLOT_BLACKOUT_TYPE_LABELS[b.type]}
        </Badge>
      ),
    },
    {
      key: "reason",
      header: "Reason",
      className: "max-w-xs",
      cell: (b) => b.reason ?? <span className="text-fg-subtle">—</span>,
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <DataTable
        title="Blackouts"
        description="Holiday and ad hoc dates a city is not bookable on (SRS 12.10.1)."
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
        rows={blackoutsQuery.data}
        rowKey={(b) => b.id}
        isLoading={blackoutsQuery.isPending}
        isFetching={blackoutsQuery.isFetching}
        error={blackoutsQuery.error}
        onRetry={() => blackoutsQuery.refetch()}
        defaultSort={{ key: "dates" }}
        caption="Blackout dates"
        emptyTitle={cityFilter ? "No blackouts in this city" : "No blackouts yet"}
        emptyDescription="Every date is bookable until a blackout is added."
        emptyAction={
          cityFilter ? (
            <Button variant="secondary" onClick={() => setCityFilter("")}>
              Show all cities
            </Button>
          ) : undefined
        }
        skeletonRows={4}
        minWidth="820px"
        maxHeight="420px"
        rowActions={
          canWrite
            ? (b) => (
                <Button
                  type="button"
                  size="sm"
                  variant="secondary"
                  disabled={deleteMutation.isPending}
                  onClick={() => setPendingDelete(b)}
                >
                  Remove
                </Button>
              )
            : undefined
        }
      />

      {canWrite ? (
        <Card
          title="Add a blackout"
          description="A blackout covers whole days — use an availability override to block a single slot."
        >
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}

            <FormGrid columns={3}>
              {/* Explicit id: see WindowsSection's matching comment - four
                  sections on this page each register a "cityId" field, and
                  without an id all four collide on `id="field-cityId"`. */}
              <Select
                id="blackout-cityId"
                label="City"
                required
                placeholder="Select a city…"
                error={form.formState.errors.cityId?.message}
                options={cityOptions}
                {...form.register("cityId")}
              />
              <Field
                label="Start date"
                type="date"
                required
                error={form.formState.errors.startDate?.message}
                {...form.register("startDate")}
              />
              <Field
                label="End date"
                type="date"
                required
                // The cross-field range check reports here, so it must render
                // on this control rather than in a detached alert.
                error={form.formState.errors.endDate?.message}
                hint="Same as the start date for a single day."
                {...form.register("endDate")}
              />
              <Select
                label="Type"
                error={form.formState.errors.type?.message}
                options={typeOptions}
                {...form.register("type")}
              />
              {/* Explicit id: OverridesSection also has a "reason" field on
                  this same page, colliding on `id="field-reason"` otherwise. */}
              <Field
                id="blackout-reason"
                label="Reason"
                hint="Optional — shown to admins only."
                error={form.formState.errors.reason?.message}
                {...form.register("reason")}
              />
            </FormGrid>

            <FormActions align="start">
              <Button type="submit" loading={createMutation.isPending}>
                Add blackout
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Remove this blackout?"
        description="The dates become bookable again as soon as it is removed."
        confirmLabel="Remove"
        cancelLabel="Keep blackout"
        loading={deleteMutation.isPending}
        error={deleteMutation.isError ? describeError(deleteMutation.error) : null}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => {
          if (pendingDelete) deleteMutation.mutate(pendingDelete.id);
        }}
      >
        {pendingDelete ? (
          <p className="text-sm text-fg-muted">
            <span className="nums font-medium text-fg">
              {pendingDelete.startDate === pendingDelete.endDate
                ? pendingDelete.startDate
                : `${pendingDelete.startDate} – ${pendingDelete.endDate}`}
            </span>{" "}
            in {pendingDelete.cityName}.
          </p>
        ) : null}
      </ConfirmDialog>
    </div>
  );
}
