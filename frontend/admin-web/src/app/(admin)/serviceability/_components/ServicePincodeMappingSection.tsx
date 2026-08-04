"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Select } from "@/components/ui";
import { FormActions, FormGrid } from "@/components/data-table";
import { EntityTable } from "@/components/entity-table";
import { describeError } from "@/lib/api";
import {
  createServicePincodeMapping,
  listPincodes,
  listServiceLookups,
  listServicePincodeMappings,
  setServicePincodeMappingActive,
} from "@/lib/serviceability-api";
import type { ServicePincodeMappingResponse } from "@/lib/serviceability-types";
import { toLookupOptions } from "./lookup-options";

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

  // The service lookup carries no activation flag; the pincode list does.
  const serviceOptions = (servicesQuery.data ?? []).map((s) => ({ value: s.id, label: s.name }));
  const pincodeOptions = toLookupOptions(pincodesQuery.data, (p) => `${p.code} · ${p.cityName}`);

  const selected = form.watch();
  /** Same duplicate guard as the category/city mapping — see its comment. */
  const duplicate = (mappingsQuery.data ?? []).find(
    (mapping) =>
      selected.serviceId !== "" &&
      mapping.serviceId === selected.serviceId &&
      mapping.pincodeId === selected.pincodeId,
  );

  const onSubmit = form.handleSubmit((values) => createMutation.mutate(values));
  const hasFilters = Boolean(serviceFilter || pincodeFilter);

  return (
    <div className="flex flex-col gap-4">
      <EntityTable<ServicePincodeMappingResponse>
        title="Service serviceability by pincode"
        description="Which services are active in which pincode (SRS 12.9.2)."
        actions={
          <div className="flex flex-wrap items-end gap-3">
            <div className="w-48">
              <Select
                label="Filter by service"
                value={serviceFilter}
                onChange={(e) => setServiceFilter(e.target.value)}
                options={[{ value: "", label: "All services" }, ...serviceOptions]}
              />
            </div>
            <div className="w-48">
              <Select
                label="Filter by pincode"
                value={pincodeFilter}
                onChange={(e) => setPincodeFilter(e.target.value)}
                options={[{ value: "", label: "All pincodes" }, ...pincodeOptions]}
              />
            </div>
          </div>
        }
        items={mappingsQuery.data}
        isLoading={mappingsQuery.isPending}
        isFetching={mappingsQuery.isFetching}
        error={mappingsQuery.error}
        onRetry={() => mappingsQuery.refetch()}
        emptyMessage={hasFilters ? "No mappings match these filters" : "No service/pincode mappings yet"}
        emptyAction={
          hasFilters ? (
            <Button
              variant="secondary"
              onClick={() => {
                setServiceFilter("");
                setPincodeFilter("");
              }}
            >
              Clear filters
            </Button>
          ) : undefined
        }
        canWrite={canWrite}
        entityLabel="service/pincode mapping"
        labelOf={(mapping) => `${mapping.serviceName} in ${mapping.pincodeCode}`}
        minWidth="680px"
        skeletonRows={5}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        toggleError={toggleMutation.error}
        onToggleActive={(mapping) => toggleMutation.mutate({ id: mapping.id, isActive: !mapping.isActive })}
        columns={[
          {
            header: "Service",
            sortValue: (mapping) => mapping.serviceName,
            render: (mapping) => <span className="font-medium text-fg">{mapping.serviceName}</span>,
          },
          {
            header: "Pincode",
            sortValue: (mapping) => mapping.pincodeCode,
            render: (mapping) => <span className="nums">{mapping.pincodeCode}</span>,
          },
        ]}
      />

      {canWrite ? (
        <Card
          title="Enable a service in a pincode"
          description="Suspending a mapping later hides the service from that pincode without deleting the record."
        >
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}
            {duplicate ? (
              <Alert tone="info">
                {duplicate.serviceName} is already mapped to {duplicate.pincodeCode}
                {duplicate.isActive ? " and is active." : " but is suspended — activate it in the list above."}
              </Alert>
            ) : null}
            <FormGrid>
              <Select
                label="Service"
                required
                placeholder="Select a service…"
                error={form.formState.errors.serviceId?.message}
                options={serviceOptions}
                {...form.register("serviceId")}
              />
              <Select
                label="Pincode"
                required
                placeholder="Select a pincode…"
                error={form.formState.errors.pincodeId?.message}
                options={pincodeOptions}
                {...form.register("pincodeId")}
              />
            </FormGrid>
            <FormActions align="start">
              <Button type="submit" loading={createMutation.isPending} disabled={duplicate !== undefined}>
                Enable serviceability
              </Button>
            </FormActions>
          </form>
        </Card>
      ) : null}
    </div>
  );
}
