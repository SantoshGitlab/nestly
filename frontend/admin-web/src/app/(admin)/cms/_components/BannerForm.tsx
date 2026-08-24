"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import type { KeyboardEvent } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Field, Select } from "@/components/ui";
import { FormActions, FormGrid } from "@/components/data-table";
import { describeError } from "@/lib/api";
import { createCmsMedia, listBannerCategories, listBannerMedia, uploadCmsMedia } from "@/lib/cms-api";
import { CmsPlacement, type BannerCreateRequest, type BannerResponse, type BannerUpdateRequest } from "@/lib/cms-types";
import { PLACEMENT_OPTIONS, datetimeLocalToUtc, utcToDatetimeLocal } from "./cmsDisplay";

/**
 * Create/edit form for a promotional banner (SRS 12.16.1, task 125a): title,
 * media asset, link, placement (with a category picker when placement is
 * CategoryPage), sort order, and an optional publish window (task
 * 124d/124e/124f). One component serves both modes - `banner` present means
 * "edit". New banners always start as Draft; the manage screen's
 * publish/unpublish toggle handles the status transition afterwards (see
 * BannersTable).
 */

const bannerFormSchema = z
  .object({
    title: z.string().min(1, "Title is required").max(200, "Title must be 200 characters or fewer"),
    subtitle: z.string().max(300, "Subtitle must be 300 characters or fewer"),
    mediaId: z.string().min(1, "An image is required"),
    linkUrl: z
      .string()
      .max(2000, "Link URL must be 2000 characters or fewer")
      // A banner whose link is a bare "about-us" resolves against whatever
      // page the banner happens to be shown on, which is never what was meant.
      .refine(
        (value) => value.trim() === "" || /^(https?:\/\/|\/)/.test(value.trim()),
        "Use a full https:// URL or a path starting with /",
      ),
    placement: z.number().int(),
    categoryId: z.string(),
    sortOrder: z.number().int().min(0, "Sort order cannot be negative"),
    publishStart: z.string(),
    publishEnd: z.string(),
  })
  .refine((values) => values.placement !== CmsPlacement.CategoryPage || values.categoryId !== "", {
    path: ["categoryId"],
    message: "A category is required when placement is Category page.",
  })
  .refine((values) => values.publishStart === "" || values.publishEnd === "" || values.publishEnd > values.publishStart, {
    path: ["publishEnd"],
    message: "Publish end must be after publish start.",
  });

type BannerFormValues = z.infer<typeof bannerFormSchema>;

function defaultValuesFor(banner: BannerResponse | null): BannerFormValues {
  if (!banner) {
    return {
      title: "",
      subtitle: "",
      mediaId: "",
      linkUrl: "",
      placement: CmsPlacement.Home,
      categoryId: "",
      sortOrder: 0,
      publishStart: "",
      publishEnd: "",
    };
  }

  return {
    title: banner.title,
    subtitle: banner.subtitle ?? "",
    mediaId: banner.mediaId,
    linkUrl: banner.linkUrl ?? "",
    placement: banner.placement,
    categoryId: banner.categoryId ?? "",
    sortOrder: banner.sortOrder,
    publishStart: utcToDatetimeLocal(banner.publishStartUtc),
    publishEnd: utcToDatetimeLocal(banner.publishEndUtc),
  };
}

export function BannerForm({
  banner,
  isSubmitting,
  submitError,
  onSubmit,
  onCancel,
}: {
  /** Present in edit mode; null when creating a new banner. */
  banner: BannerResponse | null;
  isSubmitting: boolean;
  submitError: string | null;
  onSubmit: (request: BannerCreateRequest | BannerUpdateRequest) => void;
  onCancel?: () => void;
}) {
  const queryClient = useQueryClient();
  const [newMediaUrl, setNewMediaUrl] = useState("");
  const [newMediaAlt, setNewMediaAlt] = useState("");
  const [mediaError, setMediaError] = useState<string | null>(null);

  const mediaQuery = useQuery({ queryKey: ["cms", "banners", "media"], queryFn: listBannerMedia });
  const categoriesQuery = useQuery({ queryKey: ["cms", "banners", "categories"], queryFn: listBannerCategories });

  const form = useForm<BannerFormValues>({
    resolver: zodResolver(bannerFormSchema),
    defaultValues: defaultValuesFor(banner),
  });

  // Re-seed the form whenever the banner being edited changes - react-hook-form
  // only applies `defaultValues` once, at mount (same reasoning as
  // CouponForm's identical effect).
  useEffect(() => {
    form.reset(defaultValuesFor(banner));
  }, [banner, form]);

  const placement = form.watch("placement");

  const addMediaMutation = useMutation({
    mutationFn: () => createCmsMedia({ url: newMediaUrl.trim(), altText: newMediaAlt.trim() === "" ? null : newMediaAlt.trim() }),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ["cms", "banners", "media"] });
      form.setValue("mediaId", created.id, { shouldValidate: true });
      setNewMediaUrl("");
      setNewMediaAlt("");
      setMediaError(null);
    },
    onError: (error) => setMediaError(describeError(error)),
  });

  const uploadMediaMutation = useMutation({
    mutationFn: (file: File) => uploadCmsMedia(file, newMediaAlt.trim() === "" ? null : newMediaAlt.trim()),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ["cms", "banners", "media"] });
      form.setValue("mediaId", created.id, { shouldValidate: true });
      setNewMediaAlt("");
      setMediaError(null);
    },
    onError: (error) => setMediaError(describeError(error)),
  });

  /**
   * The category picker only exists for the CategoryPage placement, but the
   * field kept its value when the placement moved away — so switching a
   * category banner to Home still posted the category id, silently scoping a
   * site-wide banner to one category. Clearing it on the change is the fix;
   * the submit guard below is the belt-and-braces half.
   */
  function onPlacementChange(next: number) {
    form.setValue("placement", next, { shouldValidate: true });
    if (next !== CmsPlacement.CategoryPage) form.setValue("categoryId", "");
  }

  const submit = form.handleSubmit((values) => {
    const isCategoryScoped = values.placement === CmsPlacement.CategoryPage;
    onSubmit({
      title: values.title.trim(),
      subtitle: values.subtitle.trim() === "" ? null : values.subtitle.trim(),
      mediaId: values.mediaId,
      linkUrl: values.linkUrl.trim() === "" ? null : values.linkUrl.trim(),
      placement: values.placement as CmsPlacement,
      categoryId: isCategoryScoped && values.categoryId !== "" ? values.categoryId : null,
      sortOrder: values.sortOrder,
      publishStartUtc: datetimeLocalToUtc(values.publishStart),
      publishEndUtc: datetimeLocalToUtc(values.publishEnd),
    });
  });

  const mediaOptions = [
    { value: "", label: mediaQuery.isPending ? "Loading images…" : "Select an image…" },
    ...(mediaQuery.data ?? []).map((media) => ({ value: media.id, label: media.altText ?? media.url })),
  ];

  const categoryOptions = [
    { value: "", label: categoriesQuery.isPending ? "Loading categories…" : "Select a category…" },
    ...(categoriesQuery.data ?? []).map((category) => ({ value: category.id, label: category.name })),
  ];

  const canAddMedia = newMediaUrl.trim() !== "" && !addMediaMutation.isPending;

  /**
   * The library sub-form is nested inside the banner form, so Enter in either
   * of its inputs would submit the *banner* — creating a half-filled banner
   * while the admin thought they were adding an image. Enter is captured and
   * routed to the sub-action instead.
   */
  const onMediaFieldKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== "Enter") return;
    event.preventDefault();
    if (canAddMedia) addMediaMutation.mutate();
  };

  return (
    <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
      {submitError ? <Alert>{submitError}</Alert> : null}
      {mediaQuery.isError ? (
        <Alert
          tone="warning"
          action={
            <Button size="sm" variant="secondary" onClick={() => mediaQuery.refetch()}>
              Retry
            </Button>
          }
        >
          The image library could not be loaded. {describeError(mediaQuery.error)}
        </Alert>
      ) : null}

      <FormGrid>
        <Field label="Title" required error={form.formState.errors.title?.message} {...form.register("title")} />
        <Field
          label="Link URL"
          hint="Optional — where tapping the banner goes."
          error={form.formState.errors.linkUrl?.message}
          {...form.register("linkUrl")}
        />
      </FormGrid>

      <Field
        label="Subtitle"
        hint="Optional — the supporting line shown beneath the title on the storefront banner."
        error={form.formState.errors.subtitle?.message}
        {...form.register("subtitle")}
      />

      <fieldset className="flex flex-col gap-3 rounded-xl border border-line bg-surface-2 p-4">
        <legend className="px-1 text-sm font-medium text-fg">Image</legend>
        <Select
          label="Image"
          required
          error={form.formState.errors.mediaId?.message}
          options={mediaOptions}
          {...form.register("mediaId")}
        />
        {mediaError ? <Alert>{mediaError}</Alert> : null}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
          <Field
            label="New image URL"
            hint="Adds to the library and selects it."
            value={newMediaUrl}
            onChange={(event) => setNewMediaUrl(event.target.value)}
            onKeyDown={onMediaFieldKeyDown}
          />
          <Field
            label="Alt text"
            hint="Optional but recommended."
            value={newMediaAlt}
            onChange={(event) => setNewMediaAlt(event.target.value)}
            onKeyDown={onMediaFieldKeyDown}
          />
          <Button
            type="button"
            variant="secondary"
            disabled={!canAddMedia}
            loading={addMediaMutation.isPending}
            onClick={() => addMediaMutation.mutate()}
          >
            Add to library
          </Button>
        </div>
        <div className="flex items-center gap-2 text-xs text-fg-subtle">
          <span className="h-px flex-1 bg-line" aria-hidden />
          or
          <span className="h-px flex-1 bg-line" aria-hidden />
        </div>
        <Field
          label={uploadMediaMutation.isPending ? "Uploading…" : "Upload an image"}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          disabled={uploadMediaMutation.isPending}
          hint="JPEG, PNG or WebP, up to 8MB. Uses the alt text above. Adds to the library and selects it."
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file) uploadMediaMutation.mutate(file);
            event.target.value = "";
          }}
        />
      </fieldset>

      <FormGrid columns={3}>
        <Select
          label="Placement"
          error={form.formState.errors.placement?.message}
          options={PLACEMENT_OPTIONS}
          name="placement"
          value={String(placement)}
          onChange={(event) => onPlacementChange(Number(event.target.value))}
        />
        {placement === CmsPlacement.CategoryPage ? (
          <Select
            label="Category"
            required
            error={form.formState.errors.categoryId?.message}
            options={categoryOptions}
            {...form.register("categoryId")}
          />
        ) : null}
        <Field
          label="Sort order"
          type="number"
          min={0}
          hint="Lower numbers appear first."
          error={form.formState.errors.sortOrder?.message}
          {...form.register("sortOrder", { valueAsNumber: true })}
        />
      </FormGrid>

      <FormGrid>
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
          {banner ? "Save changes" : "Create banner"}
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
