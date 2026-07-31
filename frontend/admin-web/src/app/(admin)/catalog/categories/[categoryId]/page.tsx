"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, PageHeading, Textarea } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { getCategory, setCategoryActive, setCategoryFeatured, updateCategory } from "@/lib/catalog-api";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { StatusBadge } from "../../../serviceability/_components/EntityTable";

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

/** Edit screen for one category (SRS 12.5, task 104): every editable field, plus activation and featuring. */
export default function EditCategoryPage() {
  const { categoryId } = useParams<{ categoryId: string }>();
  const queryClient = useQueryClient();
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "catalog");

  const categoryQuery = useQuery({ queryKey: ["categories", categoryId], queryFn: () => getCategory(categoryId) });

  const form = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    values: categoryQuery.data
      ? {
          name: categoryQuery.data.name,
          slug: categoryQuery.data.slug,
          description: categoryQuery.data.description,
          iconUrl: categoryQuery.data.iconUrl ?? "",
          bannerUrl: categoryQuery.data.bannerUrl ?? "",
          sortOrder: categoryQuery.data.sortOrder,
          seoTitle: categoryQuery.data.seoTitle ?? "",
          seoMetaDescription: categoryQuery.data.seoMetaDescription ?? "",
        }
      : undefined,
  });

  const updateMutation = useMutation({
    mutationFn: (values: CategoryFormValues) =>
      updateCategory(categoryId, {
        name: values.name,
        slug: values.slug,
        description: values.description,
        iconUrl: values.iconUrl || null,
        bannerUrl: values.bannerUrl || null,
        sortOrder: values.sortOrder,
        seoTitle: values.seoTitle || null,
        seoMetaDescription: values.seoMetaDescription || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
    },
  });

  const activeMutation = useMutation({
    mutationFn: (isActive: boolean) => setCategoryActive(categoryId, isActive),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["categories"] }),
  });

  const featuredMutation = useMutation({
    mutationFn: (isFeatured: boolean) => setCategoryFeatured(categoryId, isFeatured),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["categories"] }),
  });

  const onSubmit = form.handleSubmit((values) => updateMutation.mutate(values));

  if (categoryQuery.isLoading) {
    return <p className="text-sm text-neutral-600 dark:text-neutral-400">Loading…</p>;
  }

  if (categoryQuery.error || !categoryQuery.data) {
    return <Alert>{describeError(categoryQuery.error ?? new Error("Category not found."))}</Alert>;
  }

  const category = categoryQuery.data;

  return (
    <div className="mx-auto w-full max-w-3xl">
      <PageHeading title={`Edit category: ${category.name}`} subtitle="SRS 12.5.2 - full field set." />

      <Link href="/catalog" className="mb-4 inline-block text-sm text-neutral-600 hover:underline dark:text-neutral-400">
        ← Back to categories
      </Link>

      <Card
        title="Details"
        description="Status and featuring are changed instantly and recorded to the audit trail; other fields save together on Submit."
      >
        <div className="mb-6 flex flex-wrap items-center gap-3">
          <StatusBadge isActive={category.isActive} />
          {category.isFeatured ? (
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
                onClick={() => featuredMutation.mutate(!category.isFeatured)}
              >
                {category.isFeatured ? "Unfeature" : "Feature"}
              </Button>
              <Button
                type="button"
                variant={category.isActive ? "danger" : "secondary"}
                disabled={activeMutation.isPending}
                onClick={() => activeMutation.mutate(!category.isActive)}
              >
                {category.isActive ? "Deactivate" : "Activate"}
              </Button>
            </div>
          ) : null}
        </div>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}
          {updateMutation.isSuccess ? <Alert tone="success">Category saved.</Alert> : null}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label="Name" error={form.formState.errors.name?.message} {...form.register("name")} disabled={!canWrite} />
            <Field label="Slug" error={form.formState.errors.slug?.message} {...form.register("slug")} disabled={!canWrite} />
          </div>

          <Textarea
            label="Description"
            error={form.formState.errors.description?.message}
            {...form.register("description")}
            disabled={!canWrite}
          />

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label="Icon URL" error={form.formState.errors.iconUrl?.message} {...form.register("iconUrl")} disabled={!canWrite} />
            <Field
              label="Banner URL"
              error={form.formState.errors.bannerUrl?.message}
              {...form.register("bannerUrl")}
              disabled={!canWrite}
            />
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <Field
              label="Sort order"
              type="number"
              error={form.formState.errors.sortOrder?.message}
              {...form.register("sortOrder", { valueAsNumber: true })}
              disabled={!canWrite}
            />
            <Field
              label="SEO title"
              error={form.formState.errors.seoTitle?.message}
              {...form.register("seoTitle")}
              disabled={!canWrite}
            />
            <Field
              label="SEO meta description"
              error={form.formState.errors.seoMetaDescription?.message}
              {...form.register("seoMetaDescription")}
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
