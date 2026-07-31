"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, CheckboxField, Field, PageHeading, Select, Textarea } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { createService, listCategories, listServices, setServiceActive } from "@/lib/catalog-api";
import type { ServiceAdminResponse } from "@/lib/catalog-types";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { EntityTable } from "../../serviceability/_components/EntityTable";
import { CatalogTabs } from "../_components/CatalogTabs";

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
  sortOrder: z.number().int().min(0),
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

/**
 * Admin service/package management screen (SRS 12.6, tasks 105-106): create
 * services under a category with the full field set and option flags.
 * Editing an existing service (including its gallery) happens on its own
 * page (`/catalog/services/[id]`).
 */
export default function CatalogServicesPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  const [categoryFilter, setCategoryFilter] = useState("");
  const queryClient = useQueryClient();

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "catalog");

  const categoriesQuery = useQuery({ queryKey: ["categories"], queryFn: listCategories });
  const servicesQuery = useQuery({
    queryKey: ["services", categoryFilter],
    queryFn: () => listServices(categoryFilter || undefined),
  });

  const categoryOptions = (categoriesQuery.data ?? []).map((c) => ({ value: c.id, label: c.name }));

  const form = useForm<ServiceFormValues>({
    resolver: zodResolver(serviceSchema),
    defaultValues: {
      categoryId: "",
      name: "",
      slug: "",
      shortDescription: "",
      description: "",
      price: 0,
      durationMinutes: 60,
      inclusions: "",
      exclusions: "",
      sortOrder: 0,
      pricingType: "Fixed",
      isTaxApplicable: true,
      isAddOnAllowed: true,
      isQuantityAllowed: false,
      isInspectionBased: false,
      isSlotRequired: true,
      isAddressRequired: true,
      isCustomerNoteAllowed: true,
    },
  });

  const createMutation = useMutation({
    mutationFn: createService,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["services"] });
      form.reset({ ...form.getValues(), name: "", slug: "", description: "", shortDescription: "" });
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setServiceActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["services"] }),
  });

  const onSubmit = form.handleSubmit((values) =>
    createMutation.mutate({
      categoryId: values.categoryId,
      name: values.name,
      slug: values.slug,
      description: values.description,
      shortDescription: values.shortDescription || null,
      price: values.price,
      inclusions: values.inclusions,
      exclusions: values.exclusions,
      cancellationPolicy: null,
      reschedulePolicy: null,
      durationMinutes: values.durationMinutes,
      sortOrder: values.sortOrder,
      seoTitle: null,
      seoMetaDescription: null,
      pricingType: values.pricingType,
      isTaxApplicable: values.isTaxApplicable,
      isAddOnAllowed: values.isAddOnAllowed,
      isQuantityAllowed: values.isQuantityAllowed,
      isInspectionBased: values.isInspectionBased,
      isSlotRequired: values.isSlotRequired,
      isAddressRequired: values.isAddressRequired,
      isCustomerNoteAllowed: values.isCustomerNoteAllowed,
    }),
  );

  return (
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading title="Catalog" subtitle="Categories, services and add-ons (SRS 12.5-12.7)." />

      <CatalogTabs />

      <Card title="Services" description="Packages offered under a category, with duration, pricing and booking options (SRS 12.6).">
        <div className="mb-4 w-64">
          <Select
            label="Filter by category"
            value={categoryFilter}
            onChange={(e) => setCategoryFilter(e.target.value)}
            options={[{ value: "", label: "All categories" }, ...categoryOptions]}
          />
        </div>

        <EntityTable<ServiceAdminResponse>
          items={servicesQuery.data}
          isLoading={servicesQuery.isLoading}
          errorMessage={servicesQuery.error ? describeError(servicesQuery.error) : null}
          emptyMessage="No services yet."
          canWrite={canWrite}
          togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
          onToggleActive={(service) => toggleMutation.mutate({ id: service.id, isActive: !service.isActive })}
          columns={[
            {
              header: "Name",
              render: (service) => (
                <Link href={`/catalog/services/${service.id}`} className="font-medium underline-offset-2 hover:underline">
                  {service.name}
                </Link>
              ),
            },
            { header: "Category", render: (service) => service.categoryName },
            { header: "Price", render: (service) => `₹${service.price.toFixed(2)}` },
            { header: "Duration", render: (service) => `${service.durationMinutes} min` },
            { header: "Pricing", render: (service) => service.pricingType },
            { header: "Featured", render: (service) => (service.isFeatured ? "Yes" : "No") },
          ]}
        />

        {canWrite ? (
          <form onSubmit={onSubmit} className="mt-6 flex flex-col gap-4 border-t border-black/10 pt-6 dark:border-white/15" noValidate>
            <h3 className="text-sm font-semibold">Add service</h3>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Select
                label="Category"
                placeholder="Select a category…"
                error={form.formState.errors.categoryId?.message}
                options={categoryOptions}
                {...form.register("categoryId")}
              />
              <Field label="Name" error={form.formState.errors.name?.message} {...form.register("name")} />
            </div>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <Field label="Slug" error={form.formState.errors.slug?.message} {...form.register("slug")} />
              <Field label="Price (₹)" type="number" step="0.01" error={form.formState.errors.price?.message} {...form.register("price", { valueAsNumber: true })} />
              <Field
                label="Duration (minutes)"
                type="number"
                error={form.formState.errors.durationMinutes?.message}
                {...form.register("durationMinutes", { valueAsNumber: true })}
              />
            </div>

            <Field
              label="Short description"
              error={form.formState.errors.shortDescription?.message}
              {...form.register("shortDescription")}
            />
            <Textarea label="Description" error={form.formState.errors.description?.message} {...form.register("description")} />
            <Textarea label="Inclusions" error={form.formState.errors.inclusions?.message} {...form.register("inclusions")} />
            <Textarea label="Exclusions" error={form.formState.errors.exclusions?.message} {...form.register("exclusions")} />

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field
                label="Sort order"
                type="number"
                error={form.formState.errors.sortOrder?.message}
                {...form.register("sortOrder", { valueAsNumber: true })}
              />
              <Select
                label="Pricing type"
                options={[
                  { value: "Fixed", label: "Fixed package price" },
                  { value: "Variable", label: "Variable (base + add-ons)" },
                ]}
                {...form.register("pricingType")}
              />
            </div>

            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <CheckboxField
                label="Tax applicable"
                checked={form.watch("isTaxApplicable")}
                onChange={(v) => form.setValue("isTaxApplicable", v)}
              />
              <CheckboxField
                label="Add-ons allowed"
                checked={form.watch("isAddOnAllowed")}
                onChange={(v) => form.setValue("isAddOnAllowed", v)}
              />
              <CheckboxField
                label="Quantity allowed"
                checked={form.watch("isQuantityAllowed")}
                onChange={(v) => form.setValue("isQuantityAllowed", v)}
              />
              <CheckboxField
                label="Inspection required before scheduling"
                checked={form.watch("isInspectionBased")}
                onChange={(v) => form.setValue("isInspectionBased", v)}
              />
              <CheckboxField
                label="Slot required"
                checked={form.watch("isSlotRequired")}
                onChange={(v) => form.setValue("isSlotRequired", v)}
              />
              <CheckboxField
                label="Address required"
                checked={form.watch("isAddressRequired")}
                onChange={(v) => form.setValue("isAddressRequired", v)}
              />
              <CheckboxField
                label="Customer note allowed"
                checked={form.watch("isCustomerNoteAllowed")}
                onChange={(v) => form.setValue("isCustomerNoteAllowed", v)}
              />
            </div>

            <div>
              <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
                {createMutation.isPending ? "Adding…" : "Add service"}
              </Button>
            </div>
          </form>
        ) : null}
      </Card>
    </div>
  );
}
