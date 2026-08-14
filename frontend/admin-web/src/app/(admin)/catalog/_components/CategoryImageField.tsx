"use client";

import { useState } from "react";
import { Alert, Field } from "@/components/ui";
import { describeError } from "@/lib/api";
import { uploadCmsMedia } from "@/lib/cms-api";

/**
 * A category image field (card image / page banner): paste an already-hosted
 * URL, or upload a file directly. Reuses the CMS module's proven upload
 * endpoint (`POST /cms/media/upload`, `BannerForm.tsx`'s same pattern)
 * rather than standing up a parallel one — `Category.BannerUrl`/
 * `PageBannerUrl` are plain URL strings, not a `CmsMedia` foreign key, so
 * this only needs the upload response's `url`, never a `mediaId`.
 */
export function CategoryImageField({
  label,
  hint,
  value,
  onChange,
  error,
  disabled = false,
}: {
  label: string;
  hint?: string;
  value: string;
  onChange: (url: string) => void;
  error?: string;
  disabled?: boolean;
}) {
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const handleFile = async (file: File) => {
    setUploading(true);
    setUploadError(null);
    try {
      const media = await uploadCmsMedia(file, null);
      onChange(media.url);
    } catch (err) {
      setUploadError(describeError(err));
    } finally {
      setUploading(false);
    }
  };

  return (
    <fieldset className="flex flex-col gap-2 rounded-xl border border-line bg-surface-2 p-4">
      <legend className="px-1 text-sm font-medium text-fg">{label}</legend>
      {uploadError ? <Alert>{uploadError}</Alert> : null}

      {value ? (
        // eslint-disable-next-line @next/next/no-img-element -- admin preview of an admin-supplied URL.
        <img src={value} alt="" className="h-32 w-full rounded-lg border border-line object-cover" />
      ) : null}

      <Field
        label="Image URL"
        hint={hint}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        error={error}
        disabled={disabled}
      />

      <div className="flex items-center gap-2 text-xs text-fg-subtle">
        <span className="h-px flex-1 bg-line" aria-hidden />
        or
        <span className="h-px flex-1 bg-line" aria-hidden />
      </div>

      <Field
        label={uploading ? "Uploading…" : "Upload an image"}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        disabled={uploading || disabled}
        hint="JPEG, PNG or WebP, up to 8MB. Replaces the URL above once uploaded."
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) void handleFile(file);
          event.target.value = "";
        }}
      />
    </fieldset>
  );
}
