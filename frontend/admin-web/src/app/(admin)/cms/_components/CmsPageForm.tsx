"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Field, Select, Textarea } from "@/components/ui";
import { FormActions, FormGrid } from "@/components/data-table";
import { CmsPlacement, type CmsPageCreateRequest, type CmsPageResponse, type CmsPageUpdateRequest } from "@/lib/cms-types";
import { PLACEMENT_OPTIONS, datetimeLocalToUtc, utcToDatetimeLocal } from "./cmsDisplay";

/**
 * Create/edit form for a static CMS page (SRS 12.16.1, task 125b): title,
 * slug, body, SEO fields, placement, and an optional publish window (task
 * 124d/124f). One component serves both modes - `page` present means "edit".
 * New pages always start as Draft; the manage screen's publish/unpublish
 * toggle handles the status transition afterwards (see CmsPagesTable).
 */

const pageFormSchema = z
  .object({
    title: z.string().min(1, "Title is required").max(200, "Title must be 200 characters or fewer"),
    slug: z
      .string()
      .min(1, "Slug is required")
      .max(200, "Slug must be 200 characters or fewer")
      .regex(/^[a-z0-9]+(-[a-z0-9]+)*$/, "Slug must contain only lowercase letters, numbers, and hyphens"),
    body: z.string().min(1, "Body is required"),
    seoTitle: z.string().max(200, "SEO title must be 200 characters or fewer"),
    seoDescription: z.string().max(500, "SEO description must be 500 characters or fewer"),
    seoKeywords: z.string().max(300, "SEO keywords must be 300 characters or fewer"),
    placement: z.number().int(),
    publishStart: z.string(),
    publishEnd: z.string(),
  })
  .refine((values) => values.publishStart === "" || values.publishEnd === "" || values.publishEnd > values.publishStart, {
    path: ["publishEnd"],
    message: "Publish end must be after publish start.",
  });

type PageFormValues = z.infer<typeof pageFormSchema>;

function defaultValuesFor(page: CmsPageResponse | null): PageFormValues {
  if (!page) {
    return {
      title: "",
      slug: "",
      body: "",
      seoTitle: "",
      seoDescription: "",
      seoKeywords: "",
      placement: CmsPlacement.General,
      publishStart: "",
      publishEnd: "",
    };
  }

  return {
    title: page.title,
    slug: page.slug,
    body: page.body,
    seoTitle: page.seoTitle ?? "",
    seoDescription: page.seoDescription ?? "",
    seoKeywords: page.seoKeywords ?? "",
    placement: page.placement,
    publishStart: utcToDatetimeLocal(page.publishStartUtc),
    publishEnd: utcToDatetimeLocal(page.publishEndUtc),
  };
}

export function CmsPageForm({
  page,
  isSubmitting,
  submitError,
  onSubmit,
  onCancel,
}: {
  /** Present in edit mode; null when creating a new page. */
  page: CmsPageResponse | null;
  isSubmitting: boolean;
  submitError: string | null;
  onSubmit: (request: CmsPageCreateRequest | CmsPageUpdateRequest) => void;
  onCancel?: () => void;
}) {
  const form = useForm<PageFormValues>({
    resolver: zodResolver(pageFormSchema),
    defaultValues: defaultValuesFor(page),
  });

  // Re-seed the form whenever the page being edited changes - react-hook-form
  // only applies `defaultValues` once, at mount (same reasoning as
  // CouponForm's identical effect).
  useEffect(() => {
    form.reset(defaultValuesFor(page));
  }, [page, form]);

  const submit = form.handleSubmit((values) => {
    onSubmit({
      title: values.title.trim(),
      slug: values.slug.trim(),
      body: values.body,
      seoTitle: values.seoTitle.trim() === "" ? null : values.seoTitle.trim(),
      seoDescription: values.seoDescription.trim() === "" ? null : values.seoDescription.trim(),
      seoKeywords: values.seoKeywords.trim() === "" ? null : values.seoKeywords.trim(),
      placement: values.placement as CmsPlacement,
      publishStartUtc: datetimeLocalToUtc(values.publishStart),
      publishEndUtc: datetimeLocalToUtc(values.publishEnd),
    });
  });

  return (
    <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
      {submitError ? <Alert>{submitError}</Alert> : null}

      <FormGrid>
        <Field label="Title" required error={form.formState.errors.title?.message} {...form.register("title")} />
        <Field
          label="Slug"
          required
          placeholder="about-us"
          hint="Lowercase letters, numbers and hyphens — this becomes the customer-facing URL."
          error={form.formState.errors.slug?.message}
          {...form.register("slug")}
        />
      </FormGrid>

      <Textarea
        label="Body"
        required
        rows={8}
        error={form.formState.errors.body?.message}
        {...form.register("body")}
      />

      <FormGrid columns={3}>
        <Field label="SEO title" hint="Optional." error={form.formState.errors.seoTitle?.message} {...form.register("seoTitle")} />
        <Field
          label="SEO description"
          hint="Optional."
          error={form.formState.errors.seoDescription?.message}
          {...form.register("seoDescription")}
        />
        <Field
          label="SEO keywords"
          hint="Optional."
          error={form.formState.errors.seoKeywords?.message}
          {...form.register("seoKeywords")}
        />
      </FormGrid>

      <FormGrid columns={3}>
        <Select
          label="Placement"
          error={form.formState.errors.placement?.message}
          options={PLACEMENT_OPTIONS}
          {...form.register("placement", { valueAsNumber: true })}
        />
        <Field
          label="Publish from"
          type="datetime-local"
          hint="Optional — your local time."
          error={form.formState.errors.publishStart?.message}
          {...form.register("publishStart")}
        />
        <Field
          label="Publish until"
          type="datetime-local"
          hint="Optional — your local time."
          error={form.formState.errors.publishEnd?.message}
          {...form.register("publishEnd")}
        />
      </FormGrid>

      <FormActions align="start">
        <Button type="submit" loading={isSubmitting}>
          {page ? "Save changes" : "Create page"}
        </Button>
        {onCancel ? (
          <Button type="button" variant="secondary" onClick={onCancel} disabled={isSubmitting}>
            Cancel
          </Button>
        ) : null}
      </FormActions>
    </form>
  );
}
