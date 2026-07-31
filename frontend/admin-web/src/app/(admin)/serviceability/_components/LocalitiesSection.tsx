"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { createLocality, listLocalities, listPincodes, listZones, setLocalityActive } from "@/lib/serviceability-api";
import type { LocalityAdminResponse } from "@/lib/serviceability-types";
import { EntityTable } from "./EntityTable";

const localitySchema = z.object({
  zoneId: z.string().min(1, "Select a zone"),
  pincodeId: z.string().min(1, "Select a pincode"),
  name: z.string().min(1, "Locality name is required").max(200),
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
    mutationFn: createLocality,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["localities"] });
      form.reset({ zoneId: form.getValues("zoneId"), pincodeId: form.getValues("pincodeId"), name: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setLocalityActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["localities"] }),
  });

  const zoneOptions = (zonesQuery.data ?? []).map((zone) => ({ value: zone.id, label: zone.name }));
  const pincodeOptions = (pincodesQuery.data ?? []).map((pincode) => ({ value: pincode.id, label: pincode.code }));
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <Card title="Localities" description="The finest-grained geography master entry (SRS 12.9.1).">
      <div className="mb-4 w-64">
        <Select
          label="Filter by zone"
          value={zoneFilter}
          onChange={(e) => setZoneFilter(e.target.value)}
          options={[{ value: "", label: "All zones" }, ...zoneOptions]}
        />
      </div>

      <EntityTable<LocalityAdminResponse>
        items={localitiesQuery.data}
        isLoading={localitiesQuery.isLoading}
        errorMessage={localitiesQuery.error ? describeError(localitiesQuery.error) : null}
        emptyMessage="No localities yet."
        canWrite={canWrite}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        onToggleActive={(locality) => toggleMutation.mutate({ id: locality.id, isActive: !locality.isActive })}
        columns={[
          { header: "Name", render: (locality) => locality.name },
          { header: "Zone", render: (locality) => locality.zoneName },
          { header: "Pincode", render: (locality) => locality.pincodeCode },
        ]}
      />

      {canWrite ? (
        <form onSubmit={onSubmit} className="mt-4 flex flex-wrap items-end gap-3" noValidate>
          {createMutation.isError ? (
            <div className="w-full">
              <Alert>{describeError(createMutation.error)}</Alert>
            </div>
          ) : null}
          <div className="w-44">
            <Select
              label="Zone"
              placeholder="Select a zone…"
              error={form.formState.errors.zoneId?.message}
              options={zoneOptions}
              {...form.register("zoneId")}
            />
          </div>
          <div className="w-44">
            <Select
              label="Pincode"
              placeholder="Select a pincode…"
              error={form.formState.errors.pincodeId?.message}
              options={pincodeOptions}
              {...form.register("pincodeId")}
            />
          </div>
          <div className="w-48">
            <Field label="Name" error={form.formState.errors.name?.message} {...form.register("name")} />
          </div>
          <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
            {createMutation.isPending ? "Adding…" : "Add locality"}
          </Button>
        </form>
      ) : null}
    </Card>
  );
}
