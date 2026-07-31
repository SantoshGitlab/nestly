"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, CheckboxField, Field, PageHeading, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { getServiceAddOn, listServices, setServiceAddOnActive, updateServiceAddOn } from "@/lib/catalog-api";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { StatusBadge } from "../../../serviceability/_components/EntityTable";

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

/** Edit screen for one add-on (SRS 12.7, task 108): every field, service re-mapping and activation. */
export default function EditServiceAddOnPage() {
  const { addonId } = useParams<{ addonId: string }>();
  const queryClient = useQueryClient();
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "catalog");

  const addOnQuery = useQuery({ queryKey: ["service-addons", addonId], queryFn: () => getServiceAddOn(addonId) });
  const servicesQuery = useQuery({ queryKey: ["services"], queryFn: () => listServices() });

  const serviceOptions = (servicesQuery.data ?? []).map((s) => ({ value: s.id, label: s.name }));

  const form = useForm<AddOnFormValues>({
    resolver: zodResolver(addOnSchema),
    values: addOnQuery.data
      ? {
          serviceId: addOnQuery.data.serviceId,
          name: addOnQuery.data.name,
          description: addOnQuery.data.description ?? "",
          price: addOnQuery.data.price,
          sortOrder: addOnQuery.data.sortOrder,
          isQuantityAllowed: addOnQuery.data.isQuantityAllowed,
          isMandatory: addOnQuery.data.isMandatory,
        }
      : undefined,
  });

  const updateMutation = useMutation({
    mutationFn: (values: AddOnFormValues) =>
      updateServiceAddOn(addonId, {
        serviceId: values.serviceId,
        name: values.name,
        description: values.description || null,
        price: values.price,
        sortOrder: values.sortOrder,
        isQuantityAllowed: values.isQuantityAllowed,
        isMandatory: values.isMandatory,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["service-addons"] }),
  });

  const activeMutation = useMutation({
    mutationFn: (isActive: boolean) => setServiceAddOnActive(addonId, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["service-addons"] }),
  });

  const onSubmit = form.handleSubmit((values) => updateMutation.mutate(values));

  if (addOnQuery.isLoading) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>;
  }

  if (addOnQuery.error || !addOnQuery.data) {
    return <Alert>{describeError(addOnQuery.error ?? new Error("Add-on not found."))}</Alert>;
  }

  const addOn = addOnQuery.data;

  return (
    <div className="mx-auto w-full max-w-2xl">
      <PageHeading title={`Edit add-on: ${addOn.name}`} subtitle="SRS 12.7.2 - full field set and service mapping." />

      <Link href="/catalog/addons" className="mb-4 inline-block text-sm text-neutral-600 hover:underline dark:text-neutral-400">
        ← Back to add-ons
      </Link>

      <Card
        title="Details"
        description="Status is changed instantly and recorded to the audit trail; other fields save together on Submit."
      >
        <div className="mb-6 flex flex-wrap items-center gap-3">
          <StatusBadge isActive={addOn.isActive} />

          {canWrite ? (
            <div className="ml-auto">
              <Button
                type="button"
                variant={addOn.isActive ? "danger" : "secondary"}
                disabled={activeMutation.isPending}
                onClick={() => activeMutation.mutate(!addOn.isActive)}
              >
                {addOn.isActive ? "Deactivate" : "Activate"}
              </Button>
            </div>
          ) : null}
        </div>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}
          {updateMutation.isSuccess ? <Alert tone="success">Add-on saved.</Alert> : null}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Select
              label="Service"
              options={serviceOptions}
              error={form.formState.errors.serviceId?.message}
              {...form.register("serviceId")}
              disabled={!canWrite}
            />
            <Field label="Name" error={form.formState.errors.name?.message} {...form.register("name")} disabled={!canWrite} />
          </div>

          <Field
            label="Description"
            error={form.formState.errors.description?.message}
            {...form.register("description")}
            disabled={!canWrite}
          />

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field
              label="Price (₹)"
              type="number"
              step="0.01"
              error={form.formState.errors.price?.message}
              {...form.register("price", { valueAsNumber: true })}
              disabled={!canWrite}
            />
            <Field
              label="Sort order"
              type="number"
              error={form.formState.errors.sortOrder?.message}
              {...form.register("sortOrder", { valueAsNumber: true })}
              disabled={!canWrite}
            />
          </div>

          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            <CheckboxField
              label="Quantity allowed"
              checked={form.watch("isQuantityAllowed")}
              onChange={(v) => form.setValue("isQuantityAllowed", v)}
              disabled={!canWrite}
            />
            <CheckboxField
              label="Mandatory"
              description="Included automatically whenever the service is booked."
              checked={form.watch("isMandatory")}
              onChange={(v) => form.setValue("isMandatory", v)}
              disabled={!canWrite}
            />
          </div>

          {canWrite ? (
            <div>
              <Button type="submit" disabled={form.formState.isSubmitting || updateMutation.isPending}>
                {updateMutation.isPending ? "Saving…" : "Save changes"}
              </Button>
            </div>
          ) : null}
        </form>
      </Card>
    </div>
  );
}
