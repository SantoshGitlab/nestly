"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Field, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { createCmsMedia, listBannerCategories, listBannerMedia } from "@/lib/cms-api";
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
    mediaId: z.string().min(1, "An image is required"),
    linkUrl: z.string().max(2000, "Link URL must be 2000 characters or fewer"),
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

  const submit = form.handleSubmit((values) => {
    onSubmit({
      title: values.title.trim(),
      mediaId: values.mediaId,
      linkUrl: values.linkUrl.trim() === "" ? null : values.linkUrl.trim(),
      placement: values.placement as CmsPlacement,
      categoryId: values.categoryId === "" ? null : values.categoryId,
      sortOrder: values.sortOrder,
      publishStartUtc: datetimeLocalToUtc(values.publishStart),
      publishEndUtc: datetimeLocalToUtc(values.publishEnd),
    });
  });

  const mediaOptions = [
    { value: "", label: "Select an image…" },
    ...(mediaQuery.data ?? []).map((media) => ({ value: media.id, label: media.altText ?? media.url })),
  ];

  const categoryOptions = [
    { value: "", label: "Select a category…" },
    ...(categoriesQuery.data ?? []).map((category) => ({ value: category.id, label: category.name })),
  ];

  return (
    <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
      {submitError ? <Alert>{submitError}</Alert> : null}
      {mediaQuery.isError ? <Alert tone="info">{describeError(mediaQuery.error)}</Alert> : null}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label="Title" error={form.formState.errors.title?.message} {...form.register("title")} />
        <Field label="Link URL (optional)" error={form.formState.errors.linkUrl?.message} {...form.register("linkUrl")} />
      </div>

      <div className="flex flex-col gap-2 rounded-lg border border-black/10 p-3 dark:border-white/15">
        <Select
          label="Image"
          error={form.formState.errors.mediaId?.message}
          options={mediaOptions}
          {...form.register("mediaId")}
        />
        {mediaError ? <Alert>{mediaError}</Alert> : null}
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
          <Field label="New image URL" value={newMediaUrl} onChange={(event) => setNewMediaUrl(event.target.value)} />
          <Field label="Alt text (optional)" value={newMediaAlt} onChange={(event) => setNewMediaAlt(event.target.value)} />
          <Button
            type="button"
            variant="secondary"
            disabled={newMediaUrl.trim() === "" || addMediaMutation.isPending}
            onClick={() => addMediaMutation.mutate()}
          >
            {addMediaMutation.isPending ? "Adding…" : "Add to library"}
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Select
          label="Placement"
          error={form.formState.errors.placement?.message}
          options={PLACEMENT_OPTIONS}
          {...form.register("placement", { valueAsNumber: true })}
        />
        {placement === CmsPlacement.CategoryPage ? (
          <Select
            label="Category"
            error={form.formState.errors.categoryId?.message}
            options={categoryOptions}
            {...form.register("categoryId")}
          />
        ) : null}
        <Field
          label="Sort order"
          type="number"
          error={form.formState.errors.sortOrder?.message}
          {...form.register("sortOrder", { valueAsNumber: true })}
        />
      </div>

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
          {isSubmitting ? "Saving…" : banner ? "Save changes" : "Create banner"}
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
