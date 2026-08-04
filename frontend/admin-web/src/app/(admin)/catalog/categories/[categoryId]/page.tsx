"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Badge, Button, Card, Field, PageHeading, Textarea } from "@/components/ui";
import { Breadcrumbs, ConfirmDialog, FormActions, FormGrid } from "@/components/data-table";
import { StatusBadge } from "@/components/entity-table";
import { DetailError, DetailSkeleton } from "@/components/screen-states";
import { describeError } from "@/lib/api";
import { getCategory, setCategoryActive, setCategoryFeatured, updateCategory } from "@/lib/catalog-api";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";

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
  const claims = useAdminClaims();
  const [confirmDeactivate, setConfirmDeactivate] = useState(false);

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
    return <DetailSkeleton />;
  }

  if (categoryQuery.error || !categoryQuery.data) {
    return (
      <DetailError
        title="Category"
        breadcrumbs={breadcrumbs}
        error={categoryQuery.error}
        message={categoryQuery.error ? undefined : "This category no longer exists."}
        onRetry={() => categoryQuery.refetch()}
      />
    );
  }

  const category = categoryQuery.data;

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
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
        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
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

          <FormGrid>
            <Field label="Icon URL" error={form.formState.errors.iconUrl?.message} {...form.register("iconUrl")} disabled={!canWrite} />
            <Field
              label="Banner URL"
              error={form.formState.errors.bannerUrl?.message}
              {...form.register("bannerUrl")}
              disabled={!canWrite}
            />
          </FormGrid>

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
