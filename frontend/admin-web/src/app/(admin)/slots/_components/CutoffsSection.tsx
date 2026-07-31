"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { listSlotBookingPolicies, listSlotCityLookups, upsertSlotBookingPolicy } from "@/lib/slots-api";

const policySchema = z.object({
  cityId: z.string().min(1, "Select a city"),
  cutoffMinutes: z.coerce.number().int().min(0, "Cutoff minutes cannot be negative"),
  maxAdvanceDays: z.coerce.number().int().min(1, "Max advance days must be positive"),
});
type PolicyFormValues = z.input<typeof policySchema>;
type PolicyPayload = z.output<typeof policySchema>;

/**
 * Booking cutoffs and advance-booking limits, one policy per city (SRS
 * 12.10.1 "Cutoff rules", "Advance booking limit", task 113c). Saving is an
 * upsert - selecting a city that already has a policy pre-fills its current
 * values so editing feels the same as creating one.
 */
export function CutoffsSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();

  const citiesQuery = useQuery({ queryKey: ["slot-cities"], queryFn: listSlotCityLookups });
  const policiesQuery = useQuery({ queryKey: ["slot-booking-policies"], queryFn: listSlotBookingPolicies });

  const form = useForm<PolicyFormValues, unknown, PolicyPayload>({
    resolver: zodResolver(policySchema),
    defaultValues: { cityId: "", cutoffMinutes: 60, maxAdvanceDays: 7 },
  });

  const upsertMutation = useMutation({
    mutationFn: (values: PolicyPayload) => upsertSlotBookingPolicy(values),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["slot-booking-policies"] }),
  });

  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const policiesByCityId = new Map((policiesQuery.data ?? []).map((policy) => [policy.cityId, policy]));

  function onCityChange(cityId: string) {
    form.setValue("cityId", cityId);
    const existing = policiesByCityId.get(cityId);
    if (existing) {
      form.setValue("cutoffMinutes", existing.cutoffMinutes);
      form.setValue("maxAdvanceDays", existing.maxAdvanceDays);
    }
  }

  const onSubmit = form.handleSubmit((values) => upsertMutation.mutate(values));

  return (
    <Card title="Booking Cutoffs" description="Minimum lead time and how far ahead a slot can be booked, per city (SRS 12.10.1).">
      {policiesQuery.isLoading ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>
      ) : policiesQuery.error ? (
        <p className="text-sm text-red-600 dark:text-red-400">{describeError(policiesQuery.error)}</p>
      ) : !policiesQuery.data || policiesQuery.data.length === 0 ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">No city has a booking policy configured yet.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-full text-left text-sm">
            <thead>
              <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
                <th className="px-3 py-2 font-medium">City</th>
                <th className="px-3 py-2 font-medium">Cutoff (minutes)</th>
                <th className="px-3 py-2 font-medium">Max advance (days)</th>
              </tr>
            </thead>
            <tbody>
              {policiesQuery.data.map((policy) => (
                <tr key={policy.id} className="border-b border-black/5 last:border-0 dark:border-white/10">
                  <td className="px-3 py-2">{policy.cityName}</td>
                  <td className="px-3 py-2 tabular-nums">{policy.cutoffMinutes}</td>
                  <td className="px-3 py-2 tabular-nums">{policy.maxAdvanceDays}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {canWrite ? (
        <form onSubmit={onSubmit} className="mt-4 flex flex-wrap items-end gap-3" noValidate>
          {upsertMutation.isError ? (
            <div className="w-full">
              <Alert>{describeError(upsertMutation.error)}</Alert>
            </div>
          ) : null}
          <div className="w-48">
            <Select
              label="City"
              placeholder="Select a city…"
              error={form.formState.errors.cityId?.message}
              options={cityOptions}
              value={form.watch("cityId")}
              onChange={(e) => onCityChange(e.target.value)}
              name="cityId"
            />
          </div>
          <div className="w-40">
            <Field
              label="Cutoff (minutes)"
              type="number"
              min={0}
              error={form.formState.errors.cutoffMinutes?.message}
              {...form.register("cutoffMinutes")}
            />
          </div>
          <div className="w-40">
            <Field
              label="Max advance (days)"
              type="number"
              min={1}
              error={form.formState.errors.maxAdvanceDays?.message}
              {...form.register("maxAdvanceDays")}
            />
          </div>
          <Button type="submit" disabled={form.formState.isSubmitting || upsertMutation.isPending}>
            {upsertMutation.isPending ? "Saving…" : "Save"}
          </Button>
        </form>
      ) : null}
    </Card>
  );
}
