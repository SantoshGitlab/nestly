"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select, useToast } from "@/components/ui";
import { DataTable, FormActions, FormGrid } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { describeError } from "@/lib/api";
import { listSlotBookingPolicies, listSlotCityLookups, upsertSlotBookingPolicy } from "@/lib/slots-api";
import type { SlotBookingPolicyAdminResponse } from "@/lib/slots-types";

/**
 * Whole-number field kept as a string rather than `z.coerce.number()`.
 * `Number("")` is `0`, so a coerced schema accepts an empty cutoff box and
 * silently saves "no cutoff at all" — the opposite of a validation error.
 */
const wholeNumber = (label: string, min: number) =>
  z
    .string()
    .trim()
    .min(1, `${label} is required`)
    .regex(/^\d+$/, "Enter a whole number")
    .refine((value) => Number(value) >= min, `${label} must be at least ${min}`);

const policySchema = z.object({
  cityId: z.string().min(1, "Select a city"),
  cutoffMinutes: wholeNumber("Cutoff", 0),
  maxAdvanceDays: wholeNumber("Max advance", 1),
});
type PolicyFormValues = z.infer<typeof policySchema>;

const DEFAULT_VALUES: PolicyFormValues = { cityId: "", cutoffMinutes: "60", maxAdvanceDays: "7" };

/**
 * Booking cutoffs and advance-booking limits, one policy per city (SRS
 * 12.10.1 "Cutoff rules", "Advance booking limit", task 113c). Saving is an
 * upsert - selecting a city that already has a policy pre-fills its current
 * values so editing feels the same as creating one.
 */
export function CutoffsSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const pushToast = useToast();

  const citiesQuery = useQuery({ queryKey: ["slot-cities"], queryFn: listSlotCityLookups });
  const policiesQuery = useQuery({ queryKey: ["slot-booking-policies"], queryFn: listSlotBookingPolicies });

  const form = useForm<PolicyFormValues>({
    resolver: zodResolver(policySchema),
    defaultValues: DEFAULT_VALUES,
  });

  const upsertMutation = useMutation({
    mutationFn: (values: PolicyFormValues) =>
      upsertSlotBookingPolicy({
        cityId: values.cityId,
        cutoffMinutes: Number(values.cutoffMinutes),
        maxAdvanceDays: Number(values.maxAdvanceDays),
      }),
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ["slot-booking-policies"] });
      // This screen has no other confirmation that an upsert landed — the row
      // it edits may be off-screen in a long city list.
      pushToast("success", `Booking policy saved for ${saved.cityName}.`);
    },
  });

  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const policiesByCityId = new Map((policiesQuery.data ?? []).map((policy) => [policy.cityId, policy]));

  const selectedCityId = form.watch("cityId");
  const existingPolicy = policiesByCityId.get(selectedCityId);

  function onCityChange(cityId: string) {
    form.setValue("cityId", cityId, { shouldValidate: true });
    const existing = policiesByCityId.get(cityId);
    // Falling through to the previously loaded city's numbers would let an
    // admin save one city's cutoff onto another while believing they were
    // looking at defaults, so an unconfigured city resets explicitly.
    form.setValue("cutoffMinutes", existing ? String(existing.cutoffMinutes) : DEFAULT_VALUES.cutoffMinutes);
    form.setValue("maxAdvanceDays", existing ? String(existing.maxAdvanceDays) : DEFAULT_VALUES.maxAdvanceDays);
  }

  const onSubmit = form.handleSubmit((values) => upsertMutation.mutate(values));

  const columns: DataTableColumn<SlotBookingPolicyAdminResponse>[] = [
    { key: "city", header: "City", sortValue: (p) => p.cityName, cell: (p) => p.cityName },
    {
      key: "cutoff",
      header: "Cutoff (minutes)",
      numeric: true,
      sortValue: (p) => p.cutoffMinutes,
      cell: (p) => p.cutoffMinutes.toLocaleString("en-IN"),
    },
    {
      key: "advance",
      header: "Max advance (days)",
      numeric: true,
      sortValue: (p) => p.maxAdvanceDays,
      cell: (p) => p.maxAdvanceDays.toLocaleString("en-IN"),
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <DataTable
        title="Booking cutoffs"
        description="Minimum lead time and how far ahead a slot can be booked, per city (SRS 12.10.1)."
        columns={columns}
        rows={policiesQuery.data}
        rowKey={(p) => p.id}
        isLoading={policiesQuery.isPending}
        isFetching={policiesQuery.isFetching}
        error={policiesQuery.error}
        onRetry={() => policiesQuery.refetch()}
        defaultSort={{ key: "city" }}
        caption="Per-city booking policies"
        emptyTitle="No city has a booking policy yet"
        emptyDescription="Cities without a policy fall back to the platform defaults."
        skeletonRows={4}
        minWidth="620px"
        maxHeight="420px"
      />

      {canWrite ? (
        <Card
          title={existingPolicy ? `Update the policy for ${existingPolicy.cityName}` : "Set a booking policy"}
          description="Choosing a city that already has a policy loads its current values."
        >
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {upsertMutation.isError ? <Alert>{describeError(upsertMutation.error)}</Alert> : null}

            <FormGrid columns={3}>
              {/* Explicit id: see WindowsSection's matching comment - four
                  sections on this page each register a "cityId" field, and
                  without an id all four collide on `id="field-cityId"`. */}
              <Select
                id="cutoff-cityId"
                label="City"
                required
                placeholder="Select a city…"
                error={form.formState.errors.cityId?.message}
                options={cityOptions}
                name="cityId"
                value={selectedCityId}
                onChange={(e) => onCityChange(e.target.value)}
              />
              <Field
                label="Cutoff (minutes)"
                type="number"
                min={0}
                required
                inputMode="numeric"
                hint="How close to the slot a booking may still be made."
                error={form.formState.errors.cutoffMinutes?.message}
                {...form.register("cutoffMinutes")}
              />
              <Field
                label="Max advance (days)"
                type="number"
                min={1}
                required
                inputMode="numeric"
                hint="How far ahead slots are offered."
                error={form.formState.errors.maxAdvanceDays?.message}
                {...form.register("maxAdvanceDays")}
              />
            </FormGrid>

            <FormActions align="start">
              <Button type="submit" loading={upsertMutation.isPending}>
                {existingPolicy ? "Update policy" : "Save policy"}
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}
    </div>
  );
}
