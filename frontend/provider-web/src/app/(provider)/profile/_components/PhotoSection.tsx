"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ErrorState } from "@/components/states";
import { Alert, Badge, Button, Card, Field, Skeleton, cx, useToast } from "@/components/ui";
import { getProfile, updateProfilePhoto } from "@/lib/profile-api";
import type { BadgeTone } from "@/components/ui";
import type { ProviderPhotoModerationStatus, ProviderProfile } from "@/lib/types";

const MODERATION_LABELS: Record<ProviderPhotoModerationStatus, string> = {
  Pending: "Under review",
  Approved: "Live to customers",
  Rejected: "Rejected",
};

const MODERATION_TONES: Record<ProviderPhotoModerationStatus, BadgeTone> = {
  Pending: "warning",
  Approved: "success",
  Rejected: "danger",
};

/**
 * The API rejects anything that is not an absolute http/https URL, because
 * this value ends up in an `img src` on a customer's screen. Mirrored here so
 * the provider gets the message before a round trip, not as a 400.
 */
const photoSchema = z.object({
  photoUrl: z.union([z.url("Enter a full https:// link to an image"), z.literal("")]),
});
type PhotoFormValues = z.infer<typeof photoSchema>;

/**
 * The provider's profile photo — what a customer sees on their booking and
 * live-tracking screens once it has been approved (task 293).
 *
 * Two things this screen has to be honest about, because a provider who
 * misreads either will just re-upload:
 *
 * 1. A new or replaced photo is **not** live immediately. It goes to an admin
 *    for review, the same gate KYC documents go through, so the badge and the
 *    explanatory line lead the card rather than hiding in a footnote.
 * 2. A rejected photo keeps showing here with its reason attached — that is
 *    the only way the provider knows what to change.
 *
 * There is still no file-storage backend on the platform, so `photoUrl` is a
 * reference the provider pastes in, exactly like `KycSection`'s `fileRef`.
 * The local file picker below copies a name and uploads nothing.
 */
export function PhotoSection() {
  const queryClient = useQueryClient();
  const toast = useToast();

  const query = useQuery({ queryKey: ["provider-profile"], queryFn: getProfile });

  const form = useForm<PhotoFormValues>({
    resolver: zodResolver(photoSchema),
    defaultValues: { photoUrl: "" },
  });

  useEffect(() => {
    if (query.data) {
      form.reset({ photoUrl: query.data.photoUrl ?? "" });
    }
  }, [query.data, form]);

  const mutation = useMutation({
    mutationFn: (photoUrl: string | null) => updateProfilePhoto({ photoUrl }),
    onSuccess: (profile) => {
      queryClient.setQueryData(["provider-profile"], profile);
      toast(
        "success",
        profile.photoUrl ? "Photo sent for review." : "Photo removed.",
      );
    },
  });

  const onSubmit = form.handleSubmit((values) =>
    mutation.mutate(values.photoUrl === "" ? null : values.photoUrl),
  );

  if (query.isPending) {
    return (
      <Card title="Profile photo">
        <div className="flex items-center gap-4" aria-hidden>
          <Skeleton className="h-20 w-20 rounded-full" />
          <div className="flex-1">
            <Skeleton className="h-5 w-36" />
            <Skeleton className="mt-2 h-10 w-full rounded-xl" />
          </div>
        </div>
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Profile photo">
        <ErrorState
          title="Couldn't load your photo"
          error={query.error}
          onRetry={() => query.refetch()}
          isRetrying={query.isRefetching}
        />
      </Card>
    );
  }

  const profile = query.data;
  const status = profile.photoModerationStatus;

  return (
    <Card
      title="Profile photo"
      description="Customers see this when you're assigned their job, and while you're on your way."
    >
      <div className="flex flex-col gap-5">
        {status === "Pending" ? (
          <Alert tone="warning" title="Your photo is being reviewed">
            Customers won&apos;t see it until it&apos;s approved. Photos of a person&apos;s face
            get through fastest.
          </Alert>
        ) : null}

        {status === "Rejected" ? (
          <Alert tone="error" title="Your photo was rejected">
            {profile.photoModerationNote ?? "Upload a different photo to try again."}
          </Alert>
        ) : null}

        <div className="flex items-center gap-4">
          <PhotoPreview profile={profile} />
          <div className="min-w-0 flex-1">
            <p className="truncate text-base font-semibold text-fg">{profile.displayName}</p>
            {status ? (
              <Badge tone={MODERATION_TONES[status]} className="mt-1.5">
                {MODERATION_LABELS[status]}
              </Badge>
            ) : (
              <p className="mt-1 text-sm text-fg-muted">No photo yet</p>
            )}
          </div>
        </div>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {mutation.isError ? (
            <ErrorState title="Couldn't save your photo" error={mutation.error} />
          ) : null}

          <Field
            label="Photo link"
            placeholder="https://…"
            hint="Paste a link to your photo. File upload isn't available on the platform yet."
            error={form.formState.errors.photoUrl?.message}
            {...form.register("photoUrl")}
          />

          {/* Not wired to any upload endpoint - the platform has no file
              storage. This only copies a local file's name into the field
              above; nothing leaves the device. Same as KycSection. */}
          <div className="flex flex-col gap-1.5">
            <label htmlFor="photo-local-file" className="text-sm font-medium text-fg">
              Pick a local file
            </label>
            <input
              id="photo-local-file"
              type="file"
              accept="image/*"
              aria-describedby="photo-local-file-hint"
              onChange={(event) => {
                const fileName = event.target.files?.[0]?.name;
                if (fileName) form.setValue("photoUrl", fileName, { shouldValidate: true });
              }}
              className={cx(
                "w-full cursor-pointer rounded-lg border border-line bg-surface text-sm text-fg-muted shadow-xs outline-none transition duration-fast ease-out",
                "hover:border-line-strong focus:border-brand-600 focus:ring-2 focus:ring-brand-600/25",
                "file:mr-3 file:cursor-pointer file:border-0 file:bg-surface-3 file:px-4 file:py-2.5 file:text-sm file:font-medium file:text-fg",
              )}
            />
            <p id="photo-local-file-hint" className="text-xs text-fg-muted">
              Copies the file&apos;s name into the link above. Nothing is uploaded.
            </p>
          </div>

          <div className="flex flex-col gap-2.5 border-t border-line pt-4 sm:flex-row-reverse">
            <Button type="submit" loading={mutation.isPending} className="sm:w-auto" fullWidth>
              {profile.photoUrl ? "Replace photo" : "Save photo"}
            </Button>
            {profile.photoUrl ? (
              <Button
                type="button"
                variant="secondary"
                fullWidth
                className="sm:w-auto"
                disabled={mutation.isPending}
                onClick={() => {
                  form.reset({ photoUrl: "" });
                  mutation.mutate(null);
                }}
              >
                Remove photo
              </Button>
            ) : null}
          </div>
        </form>
      </div>
    </Card>
  );
}

/**
 * The stored photo if there is one, otherwise the same initials placeholder
 * the rest of the portal uses. Rendered even while Pending or Rejected — the
 * provider is looking at their own submission, not at what a customer sees.
 */
function PhotoPreview({ profile }: { profile: ProviderProfile }) {
  if (profile.photoUrl) {
    return (
      /* next/image is not usable here: it requires the host to be listed in
         next.config's image allowlist, and a provider-supplied URL can point
         at any host. */
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={profile.photoUrl}
        alt=""
        className="h-20 w-20 shrink-0 rounded-full border border-line object-cover"
      />
    );
  }

  return (
    <span
      aria-hidden
      className="flex h-20 w-20 shrink-0 items-center justify-center rounded-full bg-brand-50 text-xl font-semibold text-brand-700 dark:bg-brand-500/15 dark:text-brand-300"
    >
      {initialsOf(profile)}
    </span>
  );
}

/** Up to two initials from the display name, for the avatar placeholder. */
function initialsOf(profile: ProviderProfile): string {
  const source = profile.displayName.trim() || profile.legalName.trim();
  const parts = source.split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  return (parts[0][0] + (parts.length > 1 ? parts[parts.length - 1][0] : "")).toUpperCase();
}
