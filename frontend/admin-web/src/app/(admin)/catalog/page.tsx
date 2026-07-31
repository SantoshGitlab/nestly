"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, PageHeading, Textarea } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { createCategory, listCategories, setCategoryActive } from "@/lib/catalog-api";
import type { CategoryResponse } from "@/lib/catalog-types";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { EntityTable } from "../serviceability/_components/EntityTable";
import { CatalogTabs } from "./_components/CatalogTabs";

const categorySchema = z.object({
  name: z.string().min(1, "Category name is required").max(200),
  slug: z
    .string()
    .min(1, "Slug is required")
    .max(200)
    .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, "Lowercase letters, numbers and hyphens only (e.g. \"deep-cleaning\")"),
  description: z.string().max(2000),
  iconUrl: z.string().max(500).optional().or(z.literal("")),
  bannerUrl: z.string().max(500).optional().or(z.literal("")),
  sortOrder: z.number().int().min(0),
  seoTitle: z.string().max(200).optional().or(z.literal("")),
  seoMetaDescription: z.string().max(500).optional().or(z.literal("")),
});
type CategoryFormValues = z.infer<typeof categorySchema>;

/**
 * Admin category management screen (SRS 12.5, tasks 103a-104): create
 * categories and manage their display order, media and SEO fields. Editing
 * an existing category happens on its own page (`/catalog/categories/[id]`)
 * since a category carries too many fields for an inline row form.
 */
export default function CatalogCategoriesPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  const queryClient = useQueryClient();

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "catalog");

  const categoriesQuery = useQuery({ queryKey: ["categories"], queryFn: listCategories });

  const form = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: {
      name: "",
      slug: "",
      description: "",
      iconUrl: "",
      bannerUrl: "",
      sortOrder: 0,
      seoTitle: "",
      seoMetaDescription: "",
    },
  });

  const createMutation = useMutation({
    mutationFn: createCategory,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      form.reset();
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setCategoryActive(id, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["categories"] }),
  });

  const onSubmit = form.handleSubmit((values) =>
    createMutation.mutate({
      name: values.name,
      slug: values.slug,
      description: values.description,
      iconUrl: values.iconUrl || null,
      bannerUrl: values.bannerUrl || null,
      sortOrder: values.sortOrder,
      seoTitle: values.seoTitle || null,
      seoMetaDescription: values.seoMetaDescription || null,
    }),
  );

  return (
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading title="Catalog" subtitle="Categories, services and add-ons (SRS 12.5-12.7)." />

      <CatalogTabs />

      <Card title="Categories" description="Top-level catalog grouping shown to customers (SRS 12.5.1).">
        <EntityTable<CategoryResponse>
          items={categoriesQuery.data}
          isLoading={categoriesQuery.isLoading}
          errorMessage={categoriesQuery.error ? describeError(categoriesQuery.error) : null}
          emptyMessage="No categories yet."
          canWrite={canWrite}
          togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
          onToggleActive={(category) => toggleMutation.mutate({ id: category.id, isActive: !category.isActive })}
          columns={[
            {
              header: "Name",
              render: (category) => (
                <Link href={`/catalog/categories/${category.id}`} className="font-medium underline-offset-2 hover:underline">
                  {category.name}
                </Link>
              ),
            },
            { header: "Slug", render: (category) => category.slug },
            { header: "Sort order", render: (category) => category.sortOrder },
            { header: "Featured", render: (category) => (category.isFeatured ? "Yes" : "No") },
          ]}
        />

        {canWrite ? (
          <form onSubmit={onSubmit} className="mt-6 flex flex-col gap-3 border-t border-black/10 pt-6 dark:border-white/15" noValidate>
            <h3 className="text-sm font-semibold">Add category</h3>
            {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field label="Name" error={form.formState.errors.name?.message} {...form.register("name")} />
              <Field label="Slug" error={form.formState.errors.slug?.message} {...form.register("slug")} />
            </div>
            <Textarea label="Description" error={form.formState.errors.description?.message} {...form.register("description")} />
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field label="Icon URL" error={form.formState.errors.iconUrl?.message} {...form.register("iconUrl")} />
              <Field label="Banner URL" error={form.formState.errors.bannerUrl?.message} {...form.register("bannerUrl")} />
            </div>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              <Field
                label="Sort order"
                type="number"
                error={form.formState.errors.sortOrder?.message}
                {...form.register("sortOrder", { valueAsNumber: true })}
              />
              <Field label="SEO title" error={form.formState.errors.seoTitle?.message} {...form.register("seoTitle")} />
              <Field
                label="SEO meta description"
                error={form.formState.errors.seoMetaDescription?.message}
                {...form.register("seoMetaDescription")}
              />
            </div>
            <div>
              <Button type="submit" disabled={form.formState.isSubmitting || createMutation.isPending}>
                {createMutation.isPending ? "Adding…" : "Add category"}
              </Button>
            </div>
          </form>
        ) : null}
      </Card>
    </div>
  );
}
