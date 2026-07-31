"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Field, Select, Textarea } from "@/components/ui";
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

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label="Title" error={form.formState.errors.title?.message} {...form.register("title")} />
        <Field
          label="Slug"
          placeholder="about-us"
          error={form.formState.errors.slug?.message}
          {...form.register("slug")}
        />
      </div>

      <Textarea label="Body" rows={8} error={form.formState.errors.body?.message} {...form.register("body")} />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Field label="SEO title (optional)" error={form.formState.errors.seoTitle?.message} {...form.register("seoTitle")} />
        <Field
          label="SEO description (optional)"
          error={form.formState.errors.seoDescription?.message}
          {...form.register("seoDescription")}
        />
        <Field
          label="SEO keywords (optional)"
          error={form.formState.errors.seoKeywords?.message}
          {...form.register("seoKeywords")}
        />
      </div>

      <Select
        label="Placement"
        error={form.formState.errors.placement?.message}
        options={PLACEMENT_OPTIONS}
        {...form.register("placement", { valueAsNumber: true })}
      />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field
          label="Publish from (optional)"
          type="datetime-local"
          error={form.formState.errors.publishStart?.message}
          {...form.register("publishStart")}
        />
        <Field
          label="Publish until (optional)"
          type="datetime-local"
          error={form.formState.errors.publishEnd?.message}
          {...form.register("publishEnd")}
        />
      </div>

      <div className="flex gap-3">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Saving…" : page ? "Save changes" : "Create page"}
        </Button>
        {onCancel ? (
          <Button type="button" variant="secondary" onClick={onCancel} disabled={isSubmitting}>
            Cancel
          </Button>
        ) : null}
      </div>
    </form>
  );
}
