"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import {
  createServicePincodeMapping,
  listPincodes,
  listServiceLookups,
  listServicePincodeMappings,
  setServicePincodeMappingActive,
} from "@/lib/serviceability-api";
import type { ServicePincodeMappingResponse } from "@/lib/serviceability-types";
import { EntityTable } from "./EntityTable";

const mappingSchema = z.object({
  serviceId: z.string().min(1, "Select a service"),
  pincodeId: z.string().min(1, "Select a pincode"),
});
type MappingFormValues = z.infer<typeof mappingSchema>;

/**
 * Which services are active in which pincode (SRS 12.9.2). Deactivating a
 * row is the reversible "temporary service suspension" SRS 12.9.2 describes.
 */
export function ServicePincodeMappingSection({ canWrite }: { canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [serviceFilter, setServiceFilter] = useState("");
  const [pincodeFilter, setPincodeFilter] = useState("");

  const servicesQuery = useQuery({ queryKey: ["service-lookups"], queryFn: listServiceLookups });
  const pincodesQuery = useQuery({ queryKey: ["pincodes", ""], queryFn: () => listPincodes(undefined) });
  const mappingsQuery = useQuery({
    queryKey: ["service-pincode-mappings", serviceFilter, pincodeFilter],
    queryFn: () => listServicePincodeMappings(serviceFilter || undefined, pincodeFilter || undefined),
  });

  const form = useForm<MappingFormValues>({
    resolver: zodResolver(mappingSchema),
    defaultValues: { serviceId: "", pincodeId: "" },
  });

  const createMutation = useMutation({
    mutationFn: createServicePincodeMapping,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["service-pincode-mappings"] });
      form.reset({ serviceId: "", pincodeId: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setServicePincodeMappingActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["service-pincode-mappings"] }),
  });

  const serviceOptions = (servicesQuery.data ?? []).map((s) => ({ value: s.id, label: s.name }));
  const pincodeOptions = (pincodesQuery.data ?? []).map((p) => ({ value: p.id, label: p.code }));
  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));

  return (
    <Card
      title="Service serviceability by pincode"
      description="Which services are active in which pincode (SRS 12.9.2)."
    >
      <div className="mb-4 flex flex-wrap gap-3">
        <div className="w-56">
          <Select
            label="Filter by service"
            value={serviceFilter}
            onChange={(e) => setServiceFilter(e.target.value)}
            options={[{ value: "", label: "All services" }, ...serviceOptions]}
          />
        </div>
        <div className="w-56">
          <Select
            label="Filter by pincode"
            value={pincodeFilter}
            onChange={(e) => setPincodeFilter(e.target.value)}
            options={[{ value: "", label: "All pincodes" }, ...pincodeOptions]}
          />
        </div>
      </div>

      <EntityTable<ServicePincodeMappingResponse>
        items={mappingsQuery.data}
        isLoading={mappingsQuery.isLoading}
        errorMessage={mappingsQuery.error ? describeError(mappingsQuery.error) : null}
        emptyMessage="No service/pincode mappings yet."
        canWrite={canWrite}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        onToggleActive={(mapping) => toggleMutation.mutate({ id: mapping.id, isActive: !mapping.isActive })}
        columns={[
          { header: "Service", render: (mapping) => mapping.serviceName },
          { header: "Pincode", render: (mapping) => mapping.pincodeCode },
        ]}
      />

      {canWrite ? (
        <form onSubmit={onSubmit} className="mt-4 flex flex-wrap items-end gap-3" noValidate>
          {createMutation.isError ? (
            <div className="w-full">
              <Alert>{describeError(createMutation.error)}</Alert>
            </div>
          ) : null}
          <div className="w-56">
            <Select
              label="Service"
              placeholder="Select a service…"
              error={form.formState.errors.serviceId?.message}
              options={serviceOptions}
              {...form.register("serviceId")}
            />
          </div>
          <div className="w-56">
            <Select
              label="Pincode"
              placeholder="Select a pincode…"
              error={form.formState.errors.pincodeId?.message}
              options={pincodeOptions}
              {...form.register("pincodeId")}
            />
          </div>
          <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
            {createMutation.isPending ? "Saving…" : "Enable serviceability"}
          </Button>
        </form>
      ) : null}
    </Card>
  );
}
