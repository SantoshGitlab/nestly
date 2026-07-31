"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { createCity, listCities, listStates, setCityActive } from "@/lib/serviceability-api";
import type { CityAdminResponse } from "@/lib/serviceability-types";
import { EntityTable } from "./EntityTable";

const citySchema = z.object({
  stateId: z.string().min(1, "Select a state"),
  name: z.string().min(1, "City name is required").max(200),
});
type CityFormValues = z.infer<typeof citySchema>;

/** Geography master: cities (SRS 12.9.1) - the level category-serviceability is mapped against (SRS 12.9.2). */
export function CitiesSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [stateFilter, setStateFilter] = useState("");

  const statesQuery = useQuery({ queryKey: ["states"], queryFn: listStates });
  const citiesQuery = useQuery({
    queryKey: ["cities", stateFilter],
    queryFn: () => listCities(stateFilter || undefined),
  });

  const form = useForm<CityFormValues>({
    resolver: zodResolver(citySchema),
    defaultValues: { stateId: "", name: "" },
  });

  const createMutation = useMutation({
    mutationFn: createCity,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["cities"] });
      form.reset({ stateId: form.getValues("stateId"), name: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setCityActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["cities"] }),
  });

  const stateOptions = (statesQuery.data ?? []).map((state) => ({ value: state.id, label: state.name }));
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <Card title="Cities" description="Category serviceability is mapped at this level (SRS 12.9.1, 12.9.2).">
      <div className="mb-4 w-64">
        <Select
          label="Filter by state"
          value={stateFilter}
          onChange={(e) => setStateFilter(e.target.value)}
          options={[{ value: "", label: "All states" }, ...stateOptions]}
        />
      </div>

      <EntityTable<CityAdminResponse>
        items={citiesQuery.data}
        isLoading={citiesQuery.isLoading}
        errorMessage={citiesQuery.error ? describeError(citiesQuery.error) : null}
        emptyMessage="No cities yet."
        canWrite={canWrite}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        onToggleActive={(city) => toggleMutation.mutate({ id: city.id, isActive: !city.isActive })}
        columns={[
          { header: "Name", render: (city) => city.name },
          { header: "State", render: (city) => city.stateName },
        ]}
      />

      {canWrite ? (
        <form onSubmit={onSubmit} className="mt-4 flex flex-wrap items-end gap-3" noValidate>
          {createMutation.isError ? (
            <div className="w-full">
              <Alert>{describeError(createMutation.error)}</Alert>
            </div>
          ) : null}
          <div className="w-48">
            <Select
              label="State"
              placeholder="Select a state…"
              error={form.formState.errors.stateId?.message}
              options={stateOptions}
              {...form.register("stateId")}
            />
          </div>
          <div className="w-48">
            <Field label="Name" error={form.formState.errors.name?.message} {...form.register("name")} />
          </div>
          <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
            {createMutation.isPending ? "Adding…" : "Add city"}
          </Button>
        </form>
      ) : null}
    </Card>
  );
}
