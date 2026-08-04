"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { FormActions, FormGrid } from "@/components/data-table";
import { EntityTable } from "@/components/entity-table";
import { describeError } from "@/lib/api";
import { createCity, listCities, listStates, setCityActive } from "@/lib/serviceability-api";
import type { CityAdminResponse } from "@/lib/serviceability-types";
import { toLookupOptions } from "./lookup-options";

const citySchema = z.object({
  stateId: z.string().min(1, "Select a state"),
  name: z.string().trim().min(1, "City name is required").max(200),
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
    mutationFn: (values: CityFormValues) => createCity({ stateId: values.stateId, name: values.name.trim() }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["cities"] });
      // Keep the state so adding several cities in one state is not repetitive.
      form.reset({ stateId: form.getValues("stateId"), name: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setCityActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["cities"] }),
  });

  const stateOptions = toLookupOptions(statesQuery.data, (state) => state.name);
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <div className="flex flex-col gap-4">
      <EntityTable<CityAdminResponse>
        title="Cities"
        description="Category serviceability is mapped at this level (SRS 12.9.1, 12.9.2)."
        actions={
          <div className="w-56">
            <Select
              label="Filter by state"
              value={stateFilter}
              onChange={(e) => setStateFilter(e.target.value)}
              options={[{ value: "", label: "All states" }, ...stateOptions]}
            />
          </div>
        }
        items={citiesQuery.data}
        isLoading={citiesQuery.isPending}
        isFetching={citiesQuery.isFetching}
        error={citiesQuery.error}
        onRetry={() => citiesQuery.refetch()}
        emptyMessage={stateFilter ? "No cities in this state" : "No cities yet"}
        emptyAction={
          stateFilter ? (
            <Button variant="secondary" onClick={() => setStateFilter("")}>
              Show all states
            </Button>
          ) : undefined
        }
        canWrite={canWrite}
        entityLabel="city"
        labelOf={(city) => city.name}
        minWidth="640px"
        skeletonRows={5}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        toggleError={toggleMutation.error}
        onToggleActive={(city) => toggleMutation.mutate({ id: city.id, isActive: !city.isActive })}
        columns={[
          {
            header: "Name",
            sortValue: (city) => city.name,
            render: (city) => <span className="font-medium text-fg">{city.name}</span>,
          },
          { header: "State", sortValue: (city) => city.stateName, render: (city) => city.stateName },
        ]}
      />

      {canWrite ? (
        <Card title="Add a city" description="Slot windows, pincodes and zones all hang off a city.">
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}
            <FormGrid>
              <Select
                label="State"
                required
                placeholder="Select a state…"
                error={form.formState.errors.stateId?.message}
                options={stateOptions}
                {...form.register("stateId")}
              />
              <Field label="Name" required error={form.formState.errors.name?.message} {...form.register("name")} />
            </FormGrid>
            <FormActions align="start">
              <Button type="submit" loading={createMutation.isPending}>
                Add city
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}
    </div>
  );
}
