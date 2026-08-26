"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Badge, Button, Card, Field, PageHeading, Select, Textarea } from "@/components/ui";
import { Breadcrumbs, ConfirmDialog, FormActions, FormGrid } from "@/components/data-table";
import { StatusBadge } from "@/components/entity-table";
import { DetailError, DetailSkeleton } from "@/components/screen-states";
import { describeError } from "@/lib/api";
import {
  getCategory,
  listCategories,
  listCategoryChildren,
  setCategoryActive,
  setCategoryFeatured,
  updateCategory,
} from "@/lib/catalog-api";
import type { CategoryResponse } from "@/lib/catalog-types";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { CategoryImageField } from "../../_components/CategoryImageField";

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
  pageBannerUrl: z.string().max(500).optional().or(z.literal("")),
  sortOrder: z.number().int().min(0),
  seoTitle: z.string().max(200).optional().or(z.literal("")),
  seoMetaDescription: z.string().max(500).optional().or(z.literal("")),
  parentCategoryId: z.string().optional().or(z.literal("")),
});
type CategoryFormValues = z.infer<typeof categorySchema>;

/** Every category reachable by walking parentCategoryId from `rootId` (Phase 3 catalog redesign) - excluded from the parent picker so a category can't be assigned under its own descendant. */
function descendantIds(rootId: string, categories: CategoryResponse[]): Set<string> {
  const childrenByParent = new Map<string, CategoryResponse[]>();
  for (const category of categories) {
    if (!category.parentCategoryId) continue;
    childrenByParent.set(category.parentCategoryId, [...(childrenByParent.get(category.parentCategoryId) ?? []), category]);
  }

  const result = new Set<string>();
  const queue = [rootId];
  while (queue.length > 0) {
    const current = queue.shift()!;
    for (const child of childrenByParent.get(current) ?? []) {
      if (!result.has(child.id)) {
        result.add(child.id);
        queue.push(child.id);
      }
    }
  }
  return result;
}

/** Edit screen for one category (SRS 12.5, task 104): every editable field, plus activation and featuring. */
export default function EditCategoryPage() {
  const { categoryId } = useParams<{ categoryId: string }>();
  const queryClient = useQueryClient();
  const claims = useAdminClaims();
  const [confirmDeactivate, setConfirmDeactivate] = useState(false);

  const canWrite = canWriteModule(claims, "catalog");

  const categoryQuery = useQuery({ queryKey: ["categories", categoryId], queryFn: () => getCategory(categoryId) });
  const allCategoriesQuery = useQuery({ queryKey: ["categories"], queryFn: listCategories });
  const childrenQuery = useQuery({
    queryKey: ["categories", categoryId, "children"],
    queryFn: () => listCategoryChildren(categoryId),
  });

  const excludedFromParentPicker = descendantIds(categoryId, allCategoriesQuery.data ?? []);
  excludedFromParentPicker.add(categoryId);
  const parentOptions = (allCategoriesQuery.data ?? [])
    .filter((c) => !excludedFromParentPicker.has(c.id))
    .map((c) => ({ value: c.id, label: c.name }));

  const form = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    values: categoryQuery.data
      ? {
          name: categoryQuery.data.name,
          slug: categoryQuery.data.slug,
          description: categoryQuery.data.description,
          iconUrl: categoryQuery.data.iconUrl ?? "",
          bannerUrl: categoryQuery.data.bannerUrl ?? "",
          pageBannerUrl: categoryQuery.data.pageBannerUrl ?? "",
          sortOrder: categoryQuery.data.sortOrder,
          seoTitle: categoryQuery.data.seoTitle ?? "",
          seoMetaDescription: categoryQuery.data.seoMetaDescription ?? "",
          parentCategoryId: categoryQuery.data.parentCategoryId ?? "",
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
        pageBannerUrl: values.pageBannerUrl || null,
        sortOrder: values.sortOrder,
        seoTitle: values.seoTitle || null,
        seoMetaDescription: values.seoMetaDescription || null,
        parentCategoryId: values.parentCategoryId || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
    },
  });

  const activeMutation = useMutation({
    mutationFn: (isActive: boolean) => setCategoryActive(categoryId, isActive),
    onSuccess: () => {
      setConfirmDeactivate(false);
      queryClient.invalidateQueries({ queryKey: ["categories"] });
    },
  });

  const featuredMutation = useMutation({
    mutationFn: (isFeatured: boolean) => setCategoryFeatured(categoryId, isFeatured),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["categories"] }),
  });

  const onSubmit = form.handleSubmit((values) => updateMutation.mutate(values));

  const breadcrumbs = [
    { label: "Catalog", href: "/catalog" },
    { label: "Categories", href: "/catalog" },
    { label: categoryQuery.data?.name ?? "Category" },
  ];

  if (categoryQuery.isPending) {
    return <DetailSkeleton className="flex w-full max-w-5xl flex-col gap-6" />;
  }

  if (categoryQuery.error || !categoryQuery.data) {
    return (
      <DetailError
        title="Category"
        breadcrumbs={breadcrumbs}
        error={categoryQuery.error}
        message={categoryQuery.error ? undefined : "This category no longer exists."}
        onRetry={() => categoryQuery.refetch()}
        className="w-full max-w-5xl"
      />
    );
  }

  const category = categoryQuery.data;

  return (
    <div className="flex w-full max-w-5xl flex-col gap-6">
      <PageHeading
        title={category.name}
        subtitle="Category details — SRS 12.5.2, full field set."
        breadcrumbs={<Breadcrumbs items={breadcrumbs} />}
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge isActive={category.isActive} />
            {category.isFeatured ? <Badge tone="accent">Featured</Badge> : null}
          </div>
        }
      />

      {canWrite ? (
        <Card
          title="Visibility"
          description="Applied instantly and recorded to the audit trail — unlike the fields below, which save together."
        >
          <FormActions align="start">
            <Button
              type="button"
              variant="secondary"
              loading={featuredMutation.isPending}
              onClick={() => featuredMutation.mutate(!category.isFeatured)}
            >
              {category.isFeatured ? "Remove from featured" : "Mark as featured"}
            </Button>
            {category.isActive ? (
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
          {featuredMutation.isError ? (
            <div className="mt-4">
              <Alert>{describeError(featuredMutation.error)}</Alert>
            </div>
          ) : null}
        </Card>
      ) : null}

      <Card title="Details" description={canWrite ? undefined : "Read-only — you do not hold catalog write access."}>
        <form onSubmit={onSubmit} className="flex max-w-2xl flex-col gap-4" noValidate>
          {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}
          {updateMutation.isSuccess ? <Alert tone="success">Category saved.</Alert> : null}

          <FormGrid>
            <Field label="Name" required error={form.formState.errors.name?.message} {...form.register("name")} disabled={!canWrite} />
            <Field
              label="Slug"
              required
              hint="Lowercase letters, numbers and hyphens — this is the customer-facing URL."
              error={form.formState.errors.slug?.message}
              {...form.register("slug")}
              disabled={!canWrite}
            />
          </FormGrid>

          <Textarea
            label="Description"
            error={form.formState.errors.description?.message}
            {...form.register("description")}
            disabled={!canWrite}
          />

          <Field
            label="Icon URL"
            hint="Small icon — used as a fallback for subcategory chips, not shown on the category card."
            error={form.formState.errors.iconUrl?.message}
            {...form.register("iconUrl")}
            disabled={!canWrite}
          />

          <FormGrid>
            <CategoryImageField
              label="Card image"
              hint="Shown on the home page and categories listing tiles."
              value={form.watch("bannerUrl") ?? ""}
              onChange={(url) => form.setValue("bannerUrl", url, { shouldValidate: true })}
              error={form.formState.errors.bannerUrl?.message}
              disabled={!canWrite}
            />
            <CategoryImageField
              label="Page banner"
              hint="Shown on this category's page, the categories listing header, and checkout. Use a different photo from the card image above."
              value={form.watch("pageBannerUrl") ?? ""}
              onChange={(url) => form.setValue("pageBannerUrl", url, { shouldValidate: true })}
              error={form.formState.errors.pageBannerUrl?.message}
              disabled={!canWrite}
            />
          </FormGrid>

          <Select
            label="Parent category"
            hint="Leave unset for a top-level category, or choose one to nest this under a parent (Phase 3)."
            placeholder="No parent (top-level)"
            options={parentOptions}
            {...form.register("parentCategoryId")}
            disabled={!canWrite}
          />

          <FormGrid columns={3}>
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
          </FormGrid>

          {canWrite ? (
            <FormActions>
              <Button type="submit" loading={form.formState.isSubmitting || updateMutation.isPending}>
                Save changes
              </Button>
            </FormActions>
          ) : null}
        </form>
      </Card>

      <Card title="Subcategories" description="Created by picking this category as a parent when adding or editing a category.">
        {childrenQuery.data && childrenQuery.data.length > 0 ? (
          <ul className="flex flex-col gap-2">
            {childrenQuery.data.map((child) => (
              <li key={child.id}>
                <Link
                  href={`/catalog/categories/${child.id}`}
                  className="text-sm font-medium text-fg underline-offset-4 hover:text-brand-600 hover:underline dark:hover:text-brand-400"
                >
                  {child.name}
                </Link>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-sm text-fg-muted">No subcategories yet.</p>
        )}
      </Card>

      <ConfirmDialog
        open={confirmDeactivate}
        title="Deactivate this category?"
        description="It disappears from the customer app immediately, along with everything filed under it."
        confirmLabel="Deactivate"
        cancelLabel="Keep active"
        loading={activeMutation.isPending}
        error={activeMutation.isError ? describeError(activeMutation.error) : null}
        onCancel={() => setConfirmDeactivate(false)}
        onConfirm={() => activeMutation.mutate(false)}
      >
        <p className="text-sm text-fg-muted">
          Deactivating <span className="font-medium text-fg">{category.name}</span>.
        </p>
      </ConfirmDialog>
    </div>
  );
}
