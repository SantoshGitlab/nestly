"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { ConfirmDialog, DataTable, FormActions, FormGrid } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { describeError } from "@/lib/api";
import {
  createSlotAvailabilityOverride,
  deleteSlotAvailabilityOverride,
  listSlotAvailabilityOverrides,
  listSlotCategoryLookups,
  listSlotCityLookups,
  listSlotServiceLookups,
  listSlotWindows,
} from "@/lib/slots-api";
import type { SlotAvailabilityOverrideAdminResponse } from "@/lib/slots-types";

const overrideSchema = z.object({
  cityId: z.string().min(1, "Select a city"),
  date: z.string().min(1, "Date is required"),
  slotWindowId: z.string(),
  categoryId: z.string(),
  serviceId: z.string(),
  reason: z.string().trim().min(1, "Reason is required").max(500, "Keep the reason under 500 characters"),
});
type OverrideFormValues = z.infer<typeof overrideSchema>;

const emptyFormValues: OverrideFormValues = {
  cityId: "",
  date: "",
  slotWindowId: "",
  categoryId: "",
  serviceId: "",
  reason: "",
};

/** Human summary of however many of the optional scope fields were set. */
function scopeSummary(o: Pick<SlotAvailabilityOverrideAdminResponse, "slotWindowName" | "categoryName" | "serviceName">) {
  const parts = [o.slotWindowName, o.categoryName, o.serviceName].filter(Boolean);
  return parts.length === 0 ? "Entire day" : parts.join(" / ");
}

/**
 * One-off manual overrides that block computed availability for a single
 * date (SRS 12.10.2, task 113e): entire day, a selected slot, or a
 * city/category/service/date combination - however many of the optional
 * scope fields are left blank.
 */
export function OverridesSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [cityFilter, setCityFilter] = useState("");
  const [dateFilter, setDateFilter] = useState("");
  const [pendingDelete, setPendingDelete] = useState<SlotAvailabilityOverrideAdminResponse | null>(null);

  const citiesQuery = useQuery({ queryKey: ["slot-cities"], queryFn: listSlotCityLookups });
  const categoriesQuery = useQuery({ queryKey: ["slot-categories"], queryFn: listSlotCategoryLookups });
  const servicesQuery = useQuery({ queryKey: ["slot-services"], queryFn: listSlotServiceLookups });
  const overridesQuery = useQuery({
    queryKey: ["slot-overrides", cityFilter, dateFilter],
    queryFn: () => listSlotAvailabilityOverrides(cityFilter || undefined, dateFilter || undefined),
  });

  const form = useForm<OverrideFormValues>({
    resolver: zodResolver(overrideSchema),
    defaultValues: emptyFormValues,
  });
  const formCityId = form.watch("cityId");

  const windowsQuery = useQuery({
    queryKey: ["slot-windows-for-override", formCityId],
    queryFn: () => listSlotWindows(formCityId),
    enabled: formCityId.length > 0,
  });

  const createMutation = useMutation({
    mutationFn: (values: OverrideFormValues) =>
      createSlotAvailabilityOverride({
        cityId: values.cityId,
        date: values.date,
        slotWindowId: values.slotWindowId || null,
        categoryId: values.categoryId || null,
        serviceId: values.serviceId || null,
        reason: values.reason.trim(),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["slot-overrides"] });
      form.reset({ ...emptyFormValues, cityId: form.getValues("cityId") });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteSlotAvailabilityOverride,
    onSuccess: () => {
      setPendingDelete(null);
      queryClient.invalidateQueries({ queryKey: ["slot-overrides"] });
    },
  });

  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const categoryOptions = (categoriesQuery.data ?? []).map((category) => ({ value: category.id, label: category.name }));
  const serviceOptions = (servicesQuery.data ?? []).map((service) => ({ value: service.id, label: service.name }));
  const windowOptions = (windowsQuery.data ?? []).map((window) => ({ value: window.id, label: window.name }));

  /**
   * Slot windows belong to one city, so a window selected under the previous
   * city has to be dropped — otherwise the form posts city B with city A's
   * window id and the server rejects it (or worse, accepts it).
   */
  function onFormCityChange(cityId: string) {
    form.setValue("cityId", cityId, { shouldValidate: true });
    form.setValue("slotWindowId", "");
  }

  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  const columns: DataTableColumn<SlotAvailabilityOverrideAdminResponse>[] = [
    { key: "city", header: "City", sortValue: (o) => o.cityName, cell: (o) => o.cityName },
    {
      key: "date",
      header: "Date",
      sortValue: (o) => o.date,
      cell: (o) => <span className="nums whitespace-nowrap">{o.date}</span>,
    },
    {
      key: "scope",
      header: "Scope",
      sortValue: (o) => scopeSummary(o),
      cell: (o) => scopeSummary(o),
    },
    { key: "reason", header: "Reason", className: "max-w-xs", cell: (o) => o.reason },
  ];

  const hasFilters = Boolean(cityFilter || dateFilter);

  return (
    <div className="flex flex-col gap-4">
      <DataTable
        title="Availability overrides"
        description="Block a specific date, slot, or city/category/service/date combination (SRS 12.10.2)."
        actions={
          <div className="flex flex-wrap items-end gap-3">
            <div className="w-48">
              <Select
                label="Filter by city"
                value={cityFilter}
                onChange={(e) => setCityFilter(e.target.value)}
                options={[{ value: "", label: "All cities" }, ...cityOptions]}
              />
            </div>
            <div className="w-44">
              <Field
                label="Filter by date"
                type="date"
                value={dateFilter}
                onChange={(e) => setDateFilter(e.target.value)}
              />
            </div>
          </div>
        }
        columns={columns}
        rows={overridesQuery.data}
        rowKey={(o) => o.id}
        isLoading={overridesQuery.isPending}
        isFetching={overridesQuery.isFetching}
        error={overridesQuery.error}
        onRetry={() => overridesQuery.refetch()}
        defaultSort={{ key: "date" }}
        caption="Availability overrides"
        emptyTitle={hasFilters ? "No overrides match these filters" : "No availability overrides yet"}
        emptyDescription="Availability is computed from windows, blackouts and cutoffs until an override blocks it."
        emptyAction={
          hasFilters ? (
            <Button
              variant="secondary"
              onClick={() => {
                setCityFilter("");
                setDateFilter("");
              }}
            >
              Clear filters
            </Button>
          ) : undefined
        }
        skeletonRows={4}
        minWidth="880px"
        maxHeight="420px"
        rowActions={
          canWrite
            ? (o) => (
                <Button
                  type="button"
                  size="sm"
                  variant="secondary"
                  disabled={deleteMutation.isPending}
                  onClick={() => setPendingDelete(o)}
                >
                  Remove
                </Button>
              )
            : undefined
        }
      />

      {canWrite ? (
        <Card
          title="Add an override"
          description="Leave a scope field blank to block everything under it — all three blank blocks the entire day."
        >
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}
            {windowsQuery.isError ? (
              <Alert tone="warning">
                Slot windows for this city could not be loaded, so the override can only be scoped to the
                whole day. {describeError(windowsQuery.error)}
              </Alert>
            ) : null}

            <FormGrid columns={3}>
              {/* Explicit id: see WindowsSection's matching comment - four
                  sections on this page each register a "cityId" field, and
                  without an id all four collide on `id="field-cityId"`. */}
              <Select
                id="override-cityId"
                label="City"
                required
                placeholder="Select a city…"
                error={form.formState.errors.cityId?.message}
                options={cityOptions}
                name="cityId"
                value={formCityId}
                onChange={(e) => onFormCityChange(e.target.value)}
              />
              <Field
                label="Date"
                type="date"
                required
                error={form.formState.errors.date?.message}
                {...form.register("date")}
              />
              <Select
                label="Slot window"
                // A real blank option rather than `placeholder`: the kit renders
                // the placeholder as a *disabled* option, so a scope picked by
                // mistake could never be cleared back to "everything".
                options={[
                  { value: "", label: windowsQuery.isFetching ? "Loading windows…" : "Entire day" },
                  ...windowOptions,
                ]}
                disabled={!formCityId || windowsQuery.isFetching}
                hint={formCityId ? undefined : "Select a city first."}
                {...form.register("slotWindowId")}
              />
              <Select
                label="Category"
                options={[{ value: "", label: "Any category" }, ...categoryOptions]}
                {...form.register("categoryId")}
              />
              <Select
                label="Service"
                options={[{ value: "", label: "Any service" }, ...serviceOptions]}
                {...form.register("serviceId")}
              />
              {/* Explicit id: BlackoutsSection also has a "reason" field on
                  this same page, colliding on `id="field-reason"` otherwise. */}
              <Field
                id="override-reason"
                label="Reason"
                required
                hint="Recorded against the override for audit."
                error={form.formState.errors.reason?.message}
                {...form.register("reason")}
              />
            </FormGrid>

            <FormActions align="start">
              <Button type="submit" loading={createMutation.isPending}>
                Add override
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Remove this override?"
        description="Availability returns to whatever the windows, blackouts and cutoffs compute for that date."
        confirmLabel="Remove"
        cancelLabel="Keep override"
        loading={deleteMutation.isPending}
        error={deleteMutation.isError ? describeError(deleteMutation.error) : null}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => {
          if (pendingDelete) deleteMutation.mutate(pendingDelete.id);
        }}
      >
        {pendingDelete ? (
          <p className="text-sm text-fg-muted">
            <span className="nums font-medium text-fg">{pendingDelete.date}</span> in{" "}
            {pendingDelete.cityName} — {scopeSummary(pendingDelete)}.
          </p>
        ) : null}
      </ConfirmDialog>
    </div>
  );
}
