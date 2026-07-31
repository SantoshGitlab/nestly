"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { createZone, listCities, listZones, setZoneActive } from "@/lib/serviceability-api";
import type { ZoneResponse } from "@/lib/serviceability-types";
import { EntityTable } from "./EntityTable";

const zoneSchema = z.object({
  cityId: z.string().min(1, "Select a city"),
  name: z.string().min(1, "Zone name is required").max(200),
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
    mutationFn: createZone,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["zones"] });
      form.reset({ cityId: form.getValues("cityId"), name: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setZoneActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["zones"] }),
  });

  const cityOptions = (citiesQuery.data ?? []).map((city) => ({ value: city.id, label: city.name }));
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <Card title="Zones" description="Operational grouping of localities within a city (SRS 12.9.1).">
      <div className="mb-4 w-64">
        <Select
          label="Filter by city"
          value={cityFilter}
          onChange={(e) => setCityFilter(e.target.value)}
          options={[{ value: "", label: "All cities" }, ...cityOptions]}
        />
      </div>

      <EntityTable<ZoneResponse>
        items={zonesQuery.data}
        isLoading={zonesQuery.isLoading}
        errorMessage={zonesQuery.error ? describeError(zonesQuery.error) : null}
        emptyMessage="No zones yet."
        canWrite={canWrite}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        onToggleActive={(zone) => toggleMutation.mutate({ id: zone.id, isActive: !zone.isActive })}
        columns={[
          { header: "Name", render: (zone) => zone.name },
          { header: "City", render: (zone) => zone.cityName },
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
              label="City"
              placeholder="Select a city…"
              error={form.formState.errors.cityId?.message}
              options={cityOptions}
              {...form.register("cityId")}
            />
          </div>
          <div className="w-48">
            <Field label="Name" error={form.formState.errors.name?.message} {...form.register("name")} />
          </div>
          <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
            {createMutation.isPending ? "Adding…" : "Add zone"}
          </Button>
        </form>
      ) : null}
    </Card>
  );
}
