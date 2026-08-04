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
import { createLocality, listLocalities, listPincodes, listZones, setLocalityActive } from "@/lib/serviceability-api";
import type { LocalityAdminResponse } from "@/lib/serviceability-types";
import { toLookupOptions } from "./lookup-options";

const localitySchema = z.object({
  zoneId: z.string().min(1, "Select a zone"),
  pincodeId: z.string().min(1, "Select a pincode"),
  name: z.string().trim().min(1, "Locality name is required").max(200),
});
type LocalityFormValues = z.infer<typeof localitySchema>;

/**
 * Geography master: localities (SRS 12.9.1), the finest-grained entry - tied
 * to both a zone and the pincode it falls under.
 */
export function LocalitiesSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [zoneFilter, setZoneFilter] = useState("");

  const zonesQuery = useQuery({ queryKey: ["zones", ""], queryFn: () => listZones(undefined) });
  const pincodesQuery = useQuery({ queryKey: ["pincodes", ""], queryFn: () => listPincodes(undefined) });
  const localitiesQuery = useQuery({
    queryKey: ["localities", zoneFilter],
    queryFn: () => listLocalities(zoneFilter || undefined),
  });

  const form = useForm<LocalityFormValues>({
    resolver: zodResolver(localitySchema),
    defaultValues: { zoneId: "", pincodeId: "", name: "" },
  });

  const createMutation = useMutation({
    mutationFn: (values: LocalityFormValues) =>
      createLocality({ zoneId: values.zoneId, pincodeId: values.pincodeId, name: values.name.trim() }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["localities"] });
      form.reset({ zoneId: form.getValues("zoneId"), pincodeId: form.getValues("pincodeId"), name: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setLocalityActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["localities"] }),
  });

  const zoneOptions = toLookupOptions(zonesQuery.data, (zone) => `${zone.name} · ${zone.cityName}`);

  const selectedZoneId = form.watch("zoneId");
  const selectedZone = (zonesQuery.data ?? []).find((zone) => zone.id === selectedZoneId);

  /**
   * A locality's zone and its pincode both belong to a city, and nothing on
   * the form previously stopped an admin pairing a Pune zone with a Mumbai
   * pincode — a locality that then resolves to two different cities depending
   * on which side of the record you read. Narrowing the pincode list to the
   * selected zone's city makes that combination unreachable.
   */
  const pincodesInZoneCity = (pincodesQuery.data ?? []).filter(
    (pincode) => !selectedZone || pincode.cityId === selectedZone.cityId,
  );
  const pincodeOptions = toLookupOptions(pincodesInZoneCity, (pincode) => pincode.code);

  function onZoneChange(zoneId: string) {
    form.setValue("zoneId", zoneId, { shouldValidate: true });
    // The previously chosen pincode may belong to another city now.
    form.setValue("pincodeId", "");
  }

  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <div className="flex flex-col gap-4">
      <EntityTable<LocalityAdminResponse>
        title="Localities"
        description="The finest-grained geography master entry (SRS 12.9.1)."
        actions={
          <div className="w-56">
            <Select
              label="Filter by zone"
              value={zoneFilter}
              onChange={(e) => setZoneFilter(e.target.value)}
              options={[{ value: "", label: "All zones" }, ...zoneOptions]}
            />
          </div>
        }
        items={localitiesQuery.data}
        isLoading={localitiesQuery.isPending}
        isFetching={localitiesQuery.isFetching}
        error={localitiesQuery.error}
        onRetry={() => localitiesQuery.refetch()}
        emptyMessage={zoneFilter ? "No localities in this zone" : "No localities yet"}
        emptyAction={
          zoneFilter ? (
            <Button variant="secondary" onClick={() => setZoneFilter("")}>
              Show all zones
            </Button>
          ) : undefined
        }
        canWrite={canWrite}
        entityLabel="locality"
        labelOf={(locality) => locality.name}
        minWidth="720px"
        skeletonRows={5}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        toggleError={toggleMutation.error}
        onToggleActive={(locality) => toggleMutation.mutate({ id: locality.id, isActive: !locality.isActive })}
        columns={[
          {
            header: "Name",
            sortValue: (locality) => locality.name,
            render: (locality) => <span className="font-medium text-fg">{locality.name}</span>,
          },
          { header: "Zone", sortValue: (locality) => locality.zoneName, render: (locality) => locality.zoneName },
          {
            header: "Pincode",
            sortValue: (locality) => locality.pincodeCode,
            render: (locality) => <span className="nums">{locality.pincodeCode}</span>,
          },
        ]}
      />

      {canWrite ? (
        <Card
          title="Add a locality"
          description="The pincode list narrows to the selected zone's city — a locality cannot straddle two cities."
        >
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}
            <FormGrid columns={3}>
              <Select
                label="Zone"
                required
                placeholder="Select a zone…"
                error={form.formState.errors.zoneId?.message}
                options={zoneOptions}
                name="zoneId"
                value={selectedZoneId}
                onChange={(e) => onZoneChange(e.target.value)}
              />
              <Select
                label="Pincode"
                required
                placeholder="Select a pincode…"
                disabled={!selectedZoneId}
                hint={
                  !selectedZoneId
                    ? "Select a zone first."
                    : pincodeOptions.length === 0
                      ? `No pincodes exist in ${selectedZone?.cityName ?? "this city"} yet.`
                      : undefined
                }
                error={form.formState.errors.pincodeId?.message}
                options={pincodeOptions}
                {...form.register("pincodeId")}
              />
              <Field label="Name" required error={form.formState.errors.name?.message} {...form.register("name")} />
            </FormGrid>
            <FormActions align="start">
              <Button type="submit" loading={createMutation.isPending}>
                Add locality
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}
    </div>
  );
}
