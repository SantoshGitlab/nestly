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
import { createZone, listCities, listZones, setZoneActive } from "@/lib/serviceability-api";
import type { ZoneResponse } from "@/lib/serviceability-types";
import { toLookupOptions } from "./lookup-options";

const zoneSchema = z.object({
  cityId: z.string().min(1, "Select a city"),
  name: z.string().trim().min(1, "Zone name is required").max(200),
});
type ZoneFormValues = z.infer<typeof zoneSchema>;

/** Geography master: zones (SRS 12.9.1) - operational grouping of localities within a city. */
export function ZonesSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [cityFilter, setCityFilter] = useState("");

  const citiesQuery = useQuery({ queryKey: ["cities", ""], queryFn: () => listCities(undefined) });
  const zonesQuery = useQuery({
    queryKey: ["zones", cityFilter],
    queryFn: () => listZones(cityFilter || undefined),
  });

  const form = useForm<ZoneFormValues>({
    resolver: zodResolver(zoneSchema),
    defaultValues: { cityId: "", name: "" },
  });

  const createMutation = useMutation({
    mutationFn: (values: ZoneFormValues) => createZone({ cityId: values.cityId, name: values.name.trim() }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["zones"] });
      form.reset({ cityId: form.getValues("cityId"), name: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setZoneActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["zones"] }),
  });

  const cityOptions = toLookupOptions(citiesQuery.data, (city) => city.name);
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <div className="flex flex-col gap-4">
      <EntityTable<ZoneResponse>
        title="Zones"
        description="Operational grouping of localities within a city (SRS 12.9.1)."
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
        items={zonesQuery.data}
        isLoading={zonesQuery.isPending}
        isFetching={zonesQuery.isFetching}
        error={zonesQuery.error}
        onRetry={() => zonesQuery.refetch()}
        emptyMessage={cityFilter ? "No zones in this city" : "No zones yet"}
        emptyAction={
          cityFilter ? (
            <Button variant="secondary" onClick={() => setCityFilter("")}>
              Show all cities
            </Button>
          ) : undefined
        }
        canWrite={canWrite}
        entityLabel="zone"
        labelOf={(zone) => zone.name}
        minWidth="640px"
        skeletonRows={5}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        toggleError={toggleMutation.error}
        onToggleActive={(zone) => toggleMutation.mutate({ id: zone.id, isActive: !zone.isActive })}
        columns={[
          {
            header: "Name",
            sortValue: (zone) => zone.name,
            render: (zone) => <span className="font-medium text-fg">{zone.name}</span>,
          },
          { header: "City", sortValue: (zone) => zone.cityName, render: (zone) => zone.cityName },
        ]}
      />

      {canWrite ? (
        <Card title="Add a zone" description="Localities are grouped into zones for operational routing.">
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}
            <FormGrid>
              <Select
                label="City"
                required
                placeholder="Select a city…"
                error={form.formState.errors.cityId?.message}
                options={cityOptions}
                {...form.register("cityId")}
              />
              <Field label="Name" required error={form.formState.errors.name?.message} {...form.register("name")} />
            </FormGrid>
            <FormActions align="start">
              <Button type="submit" loading={createMutation.isPending}>
                Add zone
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}
    </div>
  );
}
