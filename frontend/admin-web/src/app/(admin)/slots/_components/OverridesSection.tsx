"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
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

const overrideSchema = z.object({
  cityId: z.string().min(1, "Select a city"),
  date: z.string().min(1, "Date is required"),
  slotWindowId: z.string(),
  categoryId: z.string(),
  serviceId: z.string(),
  reason: z.string().min(1, "Reason is required").max(500),
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
        reason: values.reason,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["slot-overrides"] });
      form.reset({ ...emptyFormValues, cityId: form.getValues("cityId") });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteSlotAvailabilityOverride,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["slot-overrides"] }),
  });

  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const categoryOptions = (categoriesQuery.data ?? []).map((category) => ({ value: category.id, label: category.name }));
  const serviceOptions = (servicesQuery.data ?? []).map((service) => ({ value: service.id, label: service.name }));
  const windowOptions = (windowsQuery.data ?? []).map((window) => ({ value: window.id, label: window.name }));

  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  function scopeSummary(o: { slotWindowName: string | null; categoryName: string | null; serviceName: string | null }) {
    const parts = [o.slotWindowName, o.categoryName, o.serviceName].filter(Boolean);
    return parts.length === 0 ? "Entire day" : parts.join(" / ");
  }

  return (
    <Card title="Availability Overrides" description="Block a specific date, slot, or city/category/service/date combination (SRS 12.10.2).">
      <div className="mb-4 flex flex-wrap gap-3">
        <div className="w-64">
          <Select
            label="Filter by city"
            value={cityFilter}
            onChange={(e) => setCityFilter(e.target.value)}
            options={[{ value: "", label: "All cities" }, ...cityOptions]}
          />
        </div>
        <div className="w-48">
          <Field label="Filter by date" type="date" value={dateFilter} onChange={(e) => setDateFilter(e.target.value)} />
        </div>
      </div>

      {overridesQuery.isLoading ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>
      ) : overridesQuery.error ? (
        <p className="text-sm text-red-600 dark:text-red-400">{describeError(overridesQuery.error)}</p>
      ) : !overridesQuery.data || overridesQuery.data.length === 0 ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">No availability overrides yet.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-full text-left text-sm">
            <thead>
              <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
                <th className="px-3 py-2 font-medium">City</th>
                <th className="px-3 py-2 font-medium">Date</th>
                <th className="px-3 py-2 font-medium">Scope</th>
                <th className="px-3 py-2 font-medium">Reason</th>
                {canWrite ? <th className="px-3 py-2 font-medium"><span className="sr-only">Actions</span></th> : null}
              </tr>
            </thead>
            <tbody>
              {overridesQuery.data.map((override) => (
                <tr key={override.id} className="border-b border-black/5 last:border-0 dark:border-white/10">
                  <td className="px-3 py-2">{override.cityName}</td>
                  <td className="px-3 py-2 tabular-nums">{override.date}</td>
                  <td className="px-3 py-2">{scopeSummary(override)}</td>
                  <td className="px-3 py-2">{override.reason}</td>
                  {canWrite ? (
                    <td className="px-3 py-2">
                      <Button
                        type="button"
                        variant="danger"
                        className="px-2 py-1 text-xs"
                        disabled={deleteMutation.isPending && deleteMutation.variables === override.id}
                        onClick={() => deleteMutation.mutate(override.id)}
                      >
                        {deleteMutation.isPending && deleteMutation.variables === override.id ? "Removing…" : "Remove"}
                      </Button>
                    </td>
                  ) : null}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {canWrite ? (
        <form onSubmit={onSubmit} className="mt-4 flex flex-col gap-3 border-t border-black/10 pt-4 dark:border-white/15" noValidate>
          <h3 className="text-sm font-semibold">Add an override</h3>
          {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}
          <div className="flex flex-wrap items-end gap-3">
            <div className="w-48">
              <Select
                label="City"
                placeholder="Select a city…"
                error={form.formState.errors.cityId?.message}
                options={cityOptions}
                {...form.register("cityId")}
              />
            </div>
            <div className="w-40">
              <Field label="Date" type="date" error={form.formState.errors.date?.message} {...form.register("date")} />
            </div>
            <div className="w-48">
              <Select
                label="Slot window (optional)"
                placeholder="Entire day"
                options={windowOptions}
                {...form.register("slotWindowId")}
              />
            </div>
            <div className="w-48">
              <Select
                label="Category (optional)"
                placeholder="Any category"
                options={categoryOptions}
                {...form.register("categoryId")}
              />
            </div>
            <div className="w-48">
              <Select label="Service (optional)" placeholder="Any service" options={serviceOptions} {...form.register("serviceId")} />
            </div>
            <div className="w-56">
              <Field label="Reason" error={form.formState.errors.reason?.message} {...form.register("reason")} />
            </div>
          </div>
          <div>
            <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
              {createMutation.isPending ? "Adding…" : "Add override"}
            </Button>
          </div>
        </form>
      ) : null}
    </Card>
  );
}
