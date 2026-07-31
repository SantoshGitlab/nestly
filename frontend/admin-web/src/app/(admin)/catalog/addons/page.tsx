"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, CheckboxField, Field, PageHeading, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { createServiceAddOn, listServiceAddOns, listServices, setServiceAddOnActive } from "@/lib/catalog-api";
import type { ServiceAddOnAdminResponse } from "@/lib/catalog-types";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { EntityTable } from "../../serviceability/_components/EntityTable";
import { CatalogTabs } from "../_components/CatalogTabs";

const addOnSchema = z.object({
  serviceId: z.string().min(1, "Select a service"),
  name: z.string().min(1, "Add-on name is required").max(200),
  description: z.string().max(1000).optional().or(z.literal("")),
  price: z.number().positive("Price must be greater than 0"),
  sortOrder: z.number().int().min(0),
  isQuantityAllowed: z.boolean(),
  isMandatory: z.boolean(),
});
type AddOnFormValues = z.infer<typeof addOnSchema>;

/**
 * Admin add-on management screen (SRS 12.7, tasks 107-108): create add-ons
 * mapped to a service. Editing (including re-mapping to a different service)
 * happens on its own page (`/catalog/addons/[id]`).
 */
export default function CatalogAddOnsPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  const [serviceFilter, setServiceFilter] = useState("");
  const queryClient = useQueryClient();

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "catalog");

  const servicesQuery = useQuery({ queryKey: ["services"], queryFn: () => listServices() });
  const addOnsQuery = useQuery({
    queryKey: ["service-addons", serviceFilter],
    queryFn: () => listServiceAddOns(serviceFilter || undefined),
  });

  const serviceOptions = (servicesQuery.data ?? []).map((s) => ({ value: s.id, label: s.name }));

  const form = useForm<AddOnFormValues>({
    resolver: zodResolver(addOnSchema),
    defaultValues: {
      serviceId: "",
      name: "",
      description: "",
      price: 0,
      sortOrder: 0,
      isQuantityAllowed: false,
      isMandatory: false,
    },
  });

  const createMutation = useMutation({
    mutationFn: createServiceAddOn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["service-addons"] });
      form.reset({ ...form.getValues(), name: "", description: "", price: 0 });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setServiceAddOnActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["service-addons"] }),
  });

  const onSubmit = form.handleSubmit((values) =>
    createMutation.mutate({
      serviceId: values.serviceId,
      name: values.name,
      description: values.description || null,
      price: values.price,
      sortOrder: values.sortOrder,
      isQuantityAllowed: values.isQuantityAllowed,
      isMandatory: values.isMandatory,
    }),
  );

  return (
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading title="Catalog" subtitle="Categories, services and add-ons (SRS 12.5-12.7)." />

      <CatalogTabs />

      <Card title="Add-ons" description="Optional or mandatory extras mapped to a service (SRS 12.7).">
        <div className="mb-4 w-64">
          <Select
            label="Filter by service"
            value={serviceFilter}
            onChange={(e) => setServiceFilter(e.target.value)}
            options={[{ value: "", label: "All services" }, ...serviceOptions]}
          />
        </div>

        <EntityTable<ServiceAddOnAdminResponse>
          items={addOnsQuery.data}
          isLoading={addOnsQuery.isLoading}
          errorMessage={addOnsQuery.error ? describeError(addOnsQuery.error) : null}
          emptyMessage="No add-ons yet."
          canWrite={canWrite}
          togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
          onToggleActive={(addOn) => toggleMutation.mutate({ id: addOn.id, isActive: !addOn.isActive })}
          columns={[
            {
              header: "Name",
              render: (addOn) => (
                <Link href={`/catalog/addons/${addOn.id}`} className="font-medium underline-offset-2 hover:underline">
                  {addOn.name}
                </Link>
              ),
            },
            { header: "Service", render: (addOn) => addOn.serviceName },
            { header: "Price", render: (addOn) => `₹${addOn.price.toFixed(2)}` },
            { header: "Mandatory", render: (addOn) => (addOn.isMandatory ? "Yes" : "No") },
            { header: "Quantity allowed", render: (addOn) => (addOn.isQuantityAllowed ? "Yes" : "No") },
          ]}
        />

        {canWrite ? (
          <form onSubmit={onSubmit} className="mt-6 flex flex-col gap-4 border-t border-black/10 pt-6 dark:border-white/15" noValidate>
            <h3 className="text-sm font-semibold">Add add-on</h3>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Select
                label="Service"
                placeholder="Select a service…"
                error={form.formState.errors.serviceId?.message}
                options={serviceOptions}
                {...form.register("serviceId")}
              />
              <Field label="Name" error={form.formState.errors.name?.message} {...form.register("name")} />
            </div>

            <Field label="Description" error={form.formState.errors.description?.message} {...form.register("description")} />

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field label="Price (₹)" type="number" step="0.01" error={form.formState.errors.price?.message} {...form.register("price", { valueAsNumber: true })} />
              <Field label="Sort order" type="number" error={form.formState.errors.sortOrder?.message} {...form.register("sortOrder", { valueAsNumber: true })} />
            </div>

            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <CheckboxField
                label="Quantity allowed"
                checked={form.watch("isQuantityAllowed")}
                onChange={(v) => form.setValue("isQuantityAllowed", v)}
              />
              <CheckboxField
                label="Mandatory"
                description="Included automatically whenever the service is booked."
                checked={form.watch("isMandatory")}
                onChange={(v) => form.setValue("isMandatory", v)}
              />
            </div>

            <div>
              <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
                {createMutation.isPending ? "Adding…" : "Add add-on"}
              </Button>
            </div>
          </form>
        ) : null}
      </Card>
    </div>
  );
}
