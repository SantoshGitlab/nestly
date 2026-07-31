"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, CheckboxField, Field, PageHeading, Select, Textarea } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import {
  addServiceMedia,
  getService,
  listCategories,
  listServiceMedia,
  removeServiceMedia,
  setServiceActive,
  setServiceFeatured,
  updateService,
} from "@/lib/catalog-api";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { StatusBadge } from "../../../serviceability/_components/EntityTable";

const slugPattern = /^[a-z0-9]+(-[a-z0-9]+)*$/;

const serviceSchema = z.object({
  categoryId: z.string().min(1, "Select a category"),
  name: z.string().min(1, "Service name is required").max(200),
  slug: z.string().min(1, "Slug is required").max(200).regex(slugPattern, "Lowercase letters, numbers and hyphens only"),
  shortDescription: z.string().max(500).optional().or(z.literal("")),
  description: z.string().max(2000),
  price: z.number().positive("Price must be greater than 0"),
  durationMinutes: z.number().int().positive("Duration must be greater than 0"),
  inclusions: z.string().max(4000),
  exclusions: z.string().max(4000),
  cancellationPolicy: z.string().max(2000).optional().or(z.literal("")),
  reschedulePolicy: z.string().max(2000).optional().or(z.literal("")),
  sortOrder: z.number().int().min(0),
  seoTitle: z.string().max(200).optional().or(z.literal("")),
  seoMetaDescription: z.string().max(500).optional().or(z.literal("")),
  pricingType: z.enum(["Fixed", "Variable"]),
  isTaxApplicable: z.boolean(),
  isAddOnAllowed: z.boolean(),
  isQuantityAllowed: z.boolean(),
  isInspectionBased: z.boolean(),
  isSlotRequired: z.boolean(),
  isAddressRequired: z.boolean(),
  isCustomerNoteAllowed: z.boolean(),
});
type ServiceFormValues = z.infer<typeof serviceSchema>;

const mediaSchema = z.object({ url: z.string().min(1, "Image URL is required").max(1000) });
type MediaFormValues = z.infer<typeof mediaSchema>;

/** Edit screen for one service/package (SRS 12.6, task 106): every field, option flags, gallery media, activation and featuring. */
export default function EditServicePage() {
  const { serviceId } = useParams<{ serviceId: string }>();
  const queryClient = useQueryClient();
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "catalog");

  const serviceQuery = useQuery({ queryKey: ["services", serviceId], queryFn: () => getService(serviceId) });
  const categoriesQuery = useQuery({ queryKey: ["categories"], queryFn: listCategories });
  const mediaQuery = useQuery({ queryKey: ["service-media", serviceId], queryFn: () => listServiceMedia(serviceId) });

  const categoryOptions = (categoriesQuery.data ?? []).map((c) => ({ value: c.id, label: c.name }));

  const form = useForm<ServiceFormValues>({
    resolver: zodResolver(serviceSchema),
    values: serviceQuery.data
      ? {
          categoryId: serviceQuery.data.categoryId,
          name: serviceQuery.data.name,
          slug: serviceQuery.data.slug,
          shortDescription: serviceQuery.data.shortDescription ?? "",
          description: serviceQuery.data.description,
          price: serviceQuery.data.price,
          durationMinutes: serviceQuery.data.durationMinutes,
          inclusions: serviceQuery.data.inclusions,
          exclusions: serviceQuery.data.exclusions,
          cancellationPolicy: serviceQuery.data.cancellationPolicy ?? "",
          reschedulePolicy: serviceQuery.data.reschedulePolicy ?? "",
          sortOrder: serviceQuery.data.sortOrder,
          seoTitle: serviceQuery.data.seoTitle ?? "",
          seoMetaDescription: serviceQuery.data.seoMetaDescription ?? "",
          pricingType: serviceQuery.data.pricingType,
          isTaxApplicable: serviceQuery.data.isTaxApplicable,
          isAddOnAllowed: serviceQuery.data.isAddOnAllowed,
          isQuantityAllowed: serviceQuery.data.isQuantityAllowed,
          isInspectionBased: serviceQuery.data.isInspectionBased,
          isSlotRequired: serviceQuery.data.isSlotRequired,
          isAddressRequired: serviceQuery.data.isAddressRequired,
          isCustomerNoteAllowed: serviceQuery.data.isCustomerNoteAllowed,
        }
      : undefined,
  });

  const updateMutation = useMutation({
    mutationFn: (values: ServiceFormValues) =>
      updateService(serviceId, {
        categoryId: values.categoryId,
        name: values.name,
        slug: values.slug,
        description: values.description,
        shortDescription: values.shortDescription || null,
        price: values.price,
        inclusions: values.inclusions,
        exclusions: values.exclusions,
        cancellationPolicy: values.cancellationPolicy || null,
        reschedulePolicy: values.reschedulePolicy || null,
        durationMinutes: values.durationMinutes,
        sortOrder: values.sortOrder,
        seoTitle: values.seoTitle || null,
        seoMetaDescription: values.seoMetaDescription || null,
        pricingType: values.pricingType,
        isTaxApplicable: values.isTaxApplicable,
        isAddOnAllowed: values.isAddOnAllowed,
        isQuantityAllowed: values.isQuantityAllowed,
        isInspectionBased: values.isInspectionBased,
        isSlotRequired: values.isSlotRequired,
        isAddressRequired: values.isAddressRequired,
        isCustomerNoteAllowed: values.isCustomerNoteAllowed,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["services"] }),
  });

  const activeMutation = useMutation({
    mutationFn: (isActive: boolean) => setServiceActive(serviceId, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["services"] }),
  });

  const featuredMutation = useMutation({
    mutationFn: (isFeatured: boolean) => setServiceFeatured(serviceId, isFeatured),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["services"] }),
  });

  const mediaForm = useForm<MediaFormValues>({ resolver: zodResolver(mediaSchema), defaultValues: { url: "" } });

  const addMediaMutation = useMutation({
    mutationFn: (values: MediaFormValues) => addServiceMedia(serviceId, values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["service-media", serviceId] });
      mediaForm.reset();
    },
  });

  const removeMediaMutation = useMutation({
    mutationFn: (mediaId: string) => removeServiceMedia(serviceId, mediaId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["service-media", serviceId] }),
  });

  const onSubmit = form.handleSubmit((values) => updateMutation.mutate(values));
  const onAddMedia = mediaForm.handleSubmit((values) => addMediaMutation.mutate(values));

  if (serviceQuery.isLoading) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>;
  }

  if (serviceQuery.error || !serviceQuery.data) {
    return <Alert>{describeError(serviceQuery.error ?? new Error("Service not found."))}</Alert>;
  }

  const service = serviceQuery.data;

  return (
    <div className="mx-auto w-full max-w-3xl">
      <PageHeading title={`Edit service: ${service.name}`} subtitle="SRS 12.6.2/12.6.3 - full field set and option flags." />

      <Link href="/catalog/services" className="mb-4 inline-block text-sm text-neutral-600 hover:underline dark:text-neutral-400">
        ← Back to services
      </Link>

      <Card
        title="Details"
        description="Status and featuring are changed instantly and recorded to the audit trail; other fields save together on Submit."
      >
        <div className="mb-6 flex flex-wrap items-center gap-3">
          <StatusBadge isActive={service.isActive} />
          {service.isFeatured ? (
            <span className="inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800 dark:bg-amber-950 dark:text-amber-200">
              Featured
            </span>
          ) : null}

          {canWrite ? (
            <div className="ml-auto flex gap-2">
              <Button
                type="button"
                variant="secondary"
                disabled={featuredMutation.isPending}
                onClick={() => featuredMutation.mutate(!service.isFeatured)}
              >
                {service.isFeatured ? "Unfeature" : "Feature"}
              </Button>
              <Button
                type="button"
                variant={service.isActive ? "danger" : "secondary"}
                disabled={activeMutation.isPending}
                onClick={() => activeMutation.mutate(!service.isActive)}
              >
                {service.isActive ? "Deactivate" : "Activate"}
              </Button>
            </div>
          ) : null}
        </div>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}
          {updateMutation.isSuccess ? <Alert tone="success">Service saved.</Alert> : null}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Select
              label="Category"
              options={categoryOptions}
              error={form.formState.errors.categoryId?.message}
              {...form.register("categoryId")}
              disabled={!canWrite}
            />
            <Field label="Name" error={form.formState.errors.name?.message} {...form.register("name")} disabled={!canWrite} />
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <Field label="Slug" error={form.formState.errors.slug?.message} {...form.register("slug")} disabled={!canWrite} />
            <Field
              label="Price (₹)"
              type="number"
              step="0.01"
              error={form.formState.errors.price?.message}
              {...form.register("price", { valueAsNumber: true })}
              disabled={!canWrite}
            />
            <Field
              label="Duration (minutes)"
              type="number"
              error={form.formState.errors.durationMinutes?.message}
              {...form.register("durationMinutes", { valueAsNumber: true })}
              disabled={!canWrite}
            />
          </div>

          <Field
            label="Short description"
            error={form.formState.errors.shortDescription?.message}
            {...form.register("shortDescription")}
            disabled={!canWrite}
          />
          <Textarea
            label="Description"
            error={form.formState.errors.description?.message}
            {...form.register("description")}
            disabled={!canWrite}
          />
          <Textarea
            label="Inclusions"
            error={form.formState.errors.inclusions?.message}
            {...form.register("inclusions")}
            disabled={!canWrite}
          />
          <Textarea
            label="Exclusions"
            error={form.formState.errors.exclusions?.message}
            {...form.register("exclusions")}
            disabled={!canWrite}
          />
          <Textarea
            label="Cancellation policy"
            error={form.formState.errors.cancellationPolicy?.message}
            {...form.register("cancellationPolicy")}
            disabled={!canWrite}
          />
          <Textarea
            label="Reschedule policy"
            error={form.formState.errors.reschedulePolicy?.message}
            {...form.register("reschedulePolicy")}
            disabled={!canWrite}
          />

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field
              label="Sort order"
              type="number"
              error={form.formState.errors.sortOrder?.message}
              {...form.register("sortOrder", { valueAsNumber: true })}
              disabled={!canWrite}
            />
            <Select
              label="Pricing type"
              options={[
                { value: "Fixed", label: "Fixed package price" },
                { value: "Variable", label: "Variable (base + add-ons)" },
              ]}
              {...form.register("pricingType")}
              disabled={!canWrite}
            />
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label="SEO title" error={form.formState.errors.seoTitle?.message} {...form.register("seoTitle")} disabled={!canWrite} />
            <Field
              label="SEO meta description"
              error={form.formState.errors.seoMetaDescription?.message}
              {...form.register("seoMetaDescription")}
              disabled={!canWrite}
            />
          </div>

          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            <CheckboxField
              label="Tax applicable"
              checked={form.watch("isTaxApplicable")}
              onChange={(v) => form.setValue("isTaxApplicable", v)}
              disabled={!canWrite}
            />
            <CheckboxField
              label="Add-ons allowed"
              checked={form.watch("isAddOnAllowed")}
              onChange={(v) => form.setValue("isAddOnAllowed", v)}
              disabled={!canWrite}
            />
            <CheckboxField
              label="Quantity allowed"
              checked={form.watch("isQuantityAllowed")}
              onChange={(v) => form.setValue("isQuantityAllowed", v)}
              disabled={!canWrite}
            />
            <CheckboxField
              label="Inspection required before scheduling"
              checked={form.watch("isInspectionBased")}
              onChange={(v) => form.setValue("isInspectionBased", v)}
              disabled={!canWrite}
            />
            <CheckboxField
              label="Slot required"
              checked={form.watch("isSlotRequired")}
              onChange={(v) => form.setValue("isSlotRequired", v)}
              disabled={!canWrite}
            />
            <CheckboxField
              label="Address required"
              checked={form.watch("isAddressRequired")}
              onChange={(v) => form.setValue("isAddressRequired", v)}
              disabled={!canWrite}
            />
            <CheckboxField
              label="Customer note allowed"
              checked={form.watch("isCustomerNoteAllowed")}
              onChange={(v) => form.setValue("isCustomerNoteAllowed", v)}
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

      <div className="mt-6">
        <Card title="Gallery" description="Images shown on the service detail page (SRS 12.6.2 &quot;Gallery images&quot;).">
          {mediaQuery.isLoading ? (
            <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>
          ) : mediaQuery.error ? (
            <Alert>{describeError(mediaQuery.error)}</Alert>
          ) : !mediaQuery.data || mediaQuery.data.length === 0 ? (
            <p className="text-sm text-neutral-600 dark:text-neutral-400">No gallery images yet.</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {mediaQuery.data.map((media) => (
                <li key={media.id} className="flex items-center justify-between gap-3 rounded-lg border border-black/10 px-3 py-2 text-sm dark:border-white/15">
                  <span className="truncate">{media.url}</span>
                  {canWrite ? (
                    <Button
                      type="button"
                      variant="danger"
                      className="shrink-0 px-2 py-1 text-xs"
                      disabled={removeMediaMutation.isPending && removeMediaMutation.variables === media.id}
                      onClick={() => removeMediaMutation.mutate(media.id)}
                    >
                      Remove
                    </Button>
                  ) : null}
                </li>
              ))}
            </ul>
          )}

          {canWrite ? (
            <form onSubmit={onAddMedia} className="mt-4 flex flex-wrap items-end gap-3" noValidate>
              {addMediaMutation.isError ? (
                <div className="w-full">
                  <Alert>{describeError(addMediaMutation.error)}</Alert>
                </div>
              ) : null}
              <div className="min-w-64 flex-1">
                <Field label="Image URL" error={mediaForm.formState.errors.url?.message} {...mediaForm.register("url")} />
              </div>
              <Button type="submit" disabled={mediaForm.formState.isSubmitting || addMediaMutation.isPending}>
                {addMediaMutation.isPending ? "Adding…" : "Add image"}
              </Button>
            </form>
          ) : null}
        </Card>
      </div>
    </div>
  );
}
