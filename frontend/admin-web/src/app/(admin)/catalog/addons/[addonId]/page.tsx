"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, CheckboxField, Field, PageHeading, Select } from "@/components/ui";
import { Breadcrumbs, ConfirmDialog, FormActions, FormGrid } from "@/components/data-table";
import { StatusBadge } from "@/components/entity-table";
import { DetailError, DetailSkeleton } from "@/components/screen-states";
import { describeError } from "@/lib/api";
import { getServiceAddOn, listServices, setServiceAddOnActive, updateServiceAddOn } from "@/lib/catalog-api";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";

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
  const claims = useAdminClaims();
  const [confirmDeactivate, setConfirmDeactivate] = useState(false);

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
    onSuccess: () => {
      setConfirmDeactivate(false);
      queryClient.invalidateQueries({ queryKey: ["service-addons"] });
    },
  });

  const onSubmit = form.handleSubmit((values) => updateMutation.mutate(values));

  const breadcrumbs = [
    { label: "Catalog", href: "/catalog" },
    { label: "Add-ons", href: "/catalog/addons" },
    { label: addOnQuery.data?.name ?? "Add-on" },
  ];

  if (addOnQuery.isPending) {
    return <DetailSkeleton className="mx-auto flex w-full max-w-2xl flex-col gap-6" />;
  }

  if (addOnQuery.error || !addOnQuery.data) {
    return (
      <DetailError
        title="Add-on"
        breadcrumbs={breadcrumbs}
        error={addOnQuery.error}
        message={addOnQuery.error ? undefined : "This add-on no longer exists."}
        onRetry={() => addOnQuery.refetch()}
        className="mx-auto w-full max-w-2xl"
      />
    );
  }

  const addOn = addOnQuery.data;

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <PageHeading
        title={addOn.name}
        subtitle="Add-on details — SRS 12.7.2, full field set and service mapping."
        breadcrumbs={<Breadcrumbs items={breadcrumbs} />}
        actions={<StatusBadge isActive={addOn.isActive} />}
      />

      {canWrite ? (
        <Card
          title="Visibility"
          description="Applied instantly and recorded to the audit trail — unlike the fields below, which save together."
        >
          <FormActions align="start">
            {addOn.isActive ? (
              <Button type="button" variant="danger" onClick={() => setConfirmDeactivate(true)}>
                Deactivate
              </Button>
            ) : (
              <Button
                type="button"
                variant="subtle"
                loading={activeMutation.isPending}
                onClick={() => activeMutation.mutate(true)}
              >
                Activate
              </Button>
            )}
          </FormActions>
        </Card>
      ) : null}

      <Card title="Details" description={canWrite ? undefined : "Read-only — you do not hold catalog write access."}>
        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}
          {updateMutation.isSuccess ? <Alert tone="success">Add-on saved.</Alert> : null}

          <FormGrid>
            <Select
              label="Service"
              required
              options={serviceOptions}
              error={form.formState.errors.serviceId?.message}
              {...form.register("serviceId")}
              disabled={!canWrite}
            />
            <Field label="Name" required error={form.formState.errors.name?.message} {...form.register("name")} disabled={!canWrite} />
          </FormGrid>

          <Field
            label="Description"
            error={form.formState.errors.description?.message}
            {...form.register("description")}
            disabled={!canWrite}
          />

          <FormGrid>
            <Field
              label="Price"
              type="number"
              step="0.01"
              required
              leading="₹"
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
          </FormGrid>

          <fieldset className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            <legend className="mb-2 text-sm font-medium text-fg">Add-on options</legend>
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
          </fieldset>

          {canWrite ? (
            <FormActions>
              <Button type="submit" loading={form.formState.isSubmitting || updateMutation.isPending}>
                Save changes
              </Button>
            </FormActions>
          ) : null}
        </form>
      </Card>

      <ConfirmDialog
        open={confirmDeactivate}
        title="Deactivate this add-on?"
        description="Customers stop being offered it immediately. Bookings that already include it are unaffected."
        confirmLabel="Deactivate"
        cancelLabel="Keep active"
        loading={activeMutation.isPending}
        error={activeMutation.isError ? describeError(activeMutation.error) : null}
        onCancel={() => setConfirmDeactivate(false)}
        onConfirm={() => activeMutation.mutate(false)}
      >
        <p className="text-sm text-fg-muted">
          Deactivating <span className="font-medium text-fg">{addOn.name}</span>.
        </p>
      </ConfirmDialog>
    </div>
  );
}
