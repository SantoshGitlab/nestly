"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { createSlotBlackout, deleteSlotBlackout, listSlotBlackouts, listSlotCityLookups } from "@/lib/slots-api";
import { SLOT_BLACKOUT_TYPE_LABELS, SlotBlackoutType } from "@/lib/slots-types";

const blackoutSchema = z
  .object({
    cityId: z.string().min(1, "Select a city"),
    startDate: z.string().min(1, "Start date is required"),
    endDate: z.string().min(1, "End date is required"),
    type: z.string().refine((v) => v === String(SlotBlackoutType.Holiday) || v === String(SlotBlackoutType.Blackout), "Select a type"),
    reason: z.string().max(500),
  })
  .refine((values) => values.endDate >= values.startDate, {
    message: "The start date must not be after the end date.",
    path: ["endDate"],
  });
type BlackoutFormValues = z.infer<typeof blackoutSchema>;

/** Holiday and ad hoc blackout dates a city is not bookable on (SRS 12.10.1 "Holiday blackout", task 113b). */
export function BlackoutsSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [cityFilter, setCityFilter] = useState("");

  const citiesQuery = useQuery({ queryKey: ["slot-cities"], queryFn: listSlotCityLookups });
  const blackoutsQuery = useQuery({
    queryKey: ["slot-blackouts", cityFilter],
    queryFn: () => listSlotBlackouts(cityFilter || undefined),
  });

  const form = useForm<BlackoutFormValues>({
    resolver: zodResolver(blackoutSchema),
    defaultValues: { cityId: "", startDate: "", endDate: "", type: String(SlotBlackoutType.Holiday), reason: "" },
  });

  const createMutation = useMutation({
    mutationFn: (values: BlackoutFormValues) =>
      createSlotBlackout({
        cityId: values.cityId,
        startDate: values.startDate,
        endDate: values.endDate,
        type: Number(values.type) as SlotBlackoutType,
        reason: values.reason.trim() === "" ? null : values.reason,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["slot-blackouts"] });
      form.reset({ cityId: form.getValues("cityId"), startDate: "", endDate: "", type: String(SlotBlackoutType.Holiday), reason: "" });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: deleteSlotBlackout,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["slot-blackouts"] }),
  });

  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const typeOptions = [
    { value: String(SlotBlackoutType.Holiday), label: SLOT_BLACKOUT_TYPE_LABELS[SlotBlackoutType.Holiday] },
    { value: String(SlotBlackoutType.Blackout), label: SLOT_BLACKOUT_TYPE_LABELS[SlotBlackoutType.Blackout] },
  ];
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <Card title="Blackouts" description="Holiday and ad hoc dates a city is not bookable on (SRS 12.10.1).">
      <div className="mb-4 w-64">
        <Select
          label="Filter by city"
          value={cityFilter}
          onChange={(e) => setCityFilter(e.target.value)}
          options={[{ value: "", label: "All cities" }, ...cityOptions]}
        />
      </div>

      {blackoutsQuery.isLoading ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>
      ) : blackoutsQuery.error ? (
        <p className="text-sm text-red-600 dark:text-red-400">{describeError(blackoutsQuery.error)}</p>
      ) : !blackoutsQuery.data || blackoutsQuery.data.length === 0 ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">No blackouts yet.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-full text-left text-sm">
            <thead>
              <tr className="border-b border-black/10 text-xs uppercase tracking-wide text-neutral-500 dark:border-white/15 dark:text-neutral-400">
                <th className="px-3 py-2 font-medium">City</th>
                <th className="px-3 py-2 font-medium">Dates</th>
                <th className="px-3 py-2 font-medium">Type</th>
                <th className="px-3 py-2 font-medium">Reason</th>
                {canWrite ? <th className="px-3 py-2 font-medium"><span className="sr-only">Actions</span></th> : null}
              </tr>
            </thead>
            <tbody>
              {blackoutsQuery.data.map((blackout) => (
                <tr key={blackout.id} className="border-b border-black/5 last:border-0 dark:border-white/10">
                  <td className="px-3 py-2">{blackout.cityName}</td>
                  <td className="px-3 py-2 tabular-nums">
                    {blackout.startDate === blackout.endDate ? blackout.startDate : `${blackout.startDate} – ${blackout.endDate}`}
                  </td>
                  <td className="px-3 py-2">{SLOT_BLACKOUT_TYPE_LABELS[blackout.type]}</td>
                  <td className="px-3 py-2">{blackout.reason ?? "—"}</td>
                  {canWrite ? (
                    <td className="px-3 py-2">
                      <Button
                        type="button"
                        variant="danger"
                        className="px-2 py-1 text-xs"
                        disabled={deleteMutation.isPending && deleteMutation.variables === blackout.id}
                        onClick={() => deleteMutation.mutate(blackout.id)}
                      >
                        {deleteMutation.isPending && deleteMutation.variables === blackout.id ? "Removing…" : "Remove"}
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
        <form onSubmit={onSubmit} className="mt-4 flex flex-wrap items-end gap-3" noValidate>
          {createMutation.isError ? (
            <div className="w-full">
              <Alert>{describeError(createMutation.error)}</Alert>
            </div>
          ) : null}
          {form.formState.errors.endDate ? (
            <div className="w-full">
              <Alert>{form.formState.errors.endDate.message}</Alert>
            </div>
          ) : null}
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
            <Field label="Start date" type="date" error={form.formState.errors.startDate?.message} {...form.register("startDate")} />
          </div>
          <div className="w-40">
            <Field label="End date" type="date" {...form.register("endDate")} />
          </div>
          <div className="w-36">
            <Select label="Type" options={typeOptions} {...form.register("type")} />
          </div>
          <div className="w-56">
            <Field label="Reason (optional)" {...form.register("reason")} />
          </div>
          <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
            {createMutation.isPending ? "Adding…" : "Add blackout"}
          </Button>
        </form>
      ) : null}
    </Card>
  );
}
