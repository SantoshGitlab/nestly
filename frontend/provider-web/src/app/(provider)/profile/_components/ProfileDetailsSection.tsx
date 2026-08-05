"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ErrorState } from "@/components/states";
import { Alert, Badge, Button, Card, Field, Skeleton, cx, useToast } from "@/components/ui";
import { getProfile, updateProfile } from "@/lib/profile-api";
import type { BadgeTone } from "@/components/ui";
import type { ProviderOnboardingStatus, ProviderProfile, ProviderStatus } from "@/lib/types";

const profileSchema = z.object({
  legalName: z.string().min(1, "Legal name is required").max(200),
  displayName: z.string().min(1, "Display name is required").max(100),
  email: z.union([z.email("Enter a valid email address"), z.literal("")]),
});
type ProfileFormValues = z.infer<typeof profileSchema>;

const STATUS_LABELS: Record<ProviderStatus, string> = {
  PendingVerification: "Pending verification",
  Active: "Active",
  Suspended: "Suspended",
  Deactivated: "Deactivated",
};

const STATUS_TONES: Record<ProviderStatus, BadgeTone> = {
  PendingVerification: "warning",
  Active: "success",
  Suspended: "danger",
  Deactivated: "neutral",
};

/**
 * The onboarding funnel in order (docs/PROVIDER.md). Rendered as a progress
 * track rather than a bare label: "KycSubmitted" tells a provider nothing about
 * how much is left, and how much is left is the only reason they look.
 */
const ONBOARDING_STEPS: readonly { value: ProviderOnboardingStatus; label: string }[] = [
  { value: "Registered", label: "Registered" },
  { value: "ProfileCompleted", label: "Profile" },
  { value: "KycSubmitted", label: "KYC sent" },
  { value: "KycVerified", label: "Verified" },
  { value: "Completed", label: "Live" },
];

/** View and edit legal name, display name and email, with the account's standing. */
export function ProfileDetailsSection() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [isEditing, setIsEditing] = useState(false);

  const query = useQuery({ queryKey: ["provider-profile"], queryFn: getProfile });

  const form = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { legalName: "", displayName: "", email: "" },
  });

  // Populate the form once the profile loads (and whenever it refetches
  // while not mid-edit, so a save elsewhere stays reflected).
  useEffect(() => {
    if (query.data && !isEditing) {
      form.reset({
        legalName: query.data.legalName,
        displayName: query.data.displayName,
        email: query.data.email ?? "",
      });
    }
  }, [query.data, isEditing, form]);

  const mutation = useMutation({
    mutationFn: (values: ProfileFormValues) =>
      updateProfile({
        legalName: values.legalName,
        displayName: values.displayName,
        email: values.email === "" ? undefined : values.email,
      }),
    onSuccess: (profile) => {
      queryClient.setQueryData(["provider-profile"], profile);
      setIsEditing(false);
      toast("success", "Profile saved.");
    },
  });

  const onSubmit = form.handleSubmit((values) => mutation.mutate(values));

  if (query.isPending) {
    return (
      <Card title="Profile">
        <div className="flex flex-col gap-4" aria-hidden>
          <div className="flex items-center gap-4">
            <Skeleton className="h-14 w-14 rounded-full" />
            <div className="flex-1">
              <Skeleton className="h-5 w-40" />
              <Skeleton className="mt-2 h-3.5 w-28" />
            </div>
          </div>
          <Skeleton className="h-10 w-full rounded-xl" />
          <Skeleton className="h-16 w-full rounded-xl" />
        </div>
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Profile">
        <ErrorState
          title="Couldn't load your profile"
          error={query.error}
          onRetry={() => query.refetch()}
          isRetrying={query.isRefetching}
        />
      </Card>
    );
  }

  const profile = query.data;

  return (
    <Card
      title="Profile"
      description="Your legal identity and how you appear to customers."
      actions={
        !isEditing ? (
          <Button variant="secondary" size="sm" onClick={() => setIsEditing(true)}>
            Edit
          </Button>
        ) : null
      }
    >
      <div className="flex flex-col gap-5">
        {profile.status === "Suspended" || profile.status === "Deactivated" ? (
          <Alert tone="warning" title={`Your account is ${STATUS_LABELS[profile.status].toLowerCase()}`}>
            You won&apos;t be matched to new jobs while this is the case. Contact support to sort it
            out.
          </Alert>
        ) : null}

        {isEditing ? (
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {mutation.isError ? (
              <ErrorState title="Couldn't save your profile" error={mutation.error} />
            ) : null}
            <Field
              label="Legal name"
              hint="As printed on your ID — this is what we verify against."
              error={form.formState.errors.legalName?.message}
              {...form.register("legalName")}
            />
            <Field
              label="Display name"
              hint="What customers see when you're assigned a job."
              error={form.formState.errors.displayName?.message}
              {...form.register("displayName")}
            />
            <Field
              label="Email (optional)"
              type="email"
              inputMode="email"
              error={form.formState.errors.email?.message}
              {...form.register("email")}
            />
            <div className="flex flex-col gap-2.5 border-t border-line pt-4 sm:flex-row-reverse">
              <Button type="submit" loading={mutation.isPending} className="sm:w-auto" fullWidth>
                Save changes
              </Button>
              <Button
                type="button"
                variant="secondary"
                fullWidth
                className="sm:w-auto"
                disabled={mutation.isPending}
                onClick={() => {
                  setIsEditing(false);
                  mutation.reset();
                  form.reset({
                    legalName: profile.legalName,
                    displayName: profile.displayName,
                    email: profile.email ?? "",
                  });
                }}
              >
                Cancel
              </Button>
            </div>
          </form>
        ) : (
          <>
            <div className="flex items-center gap-4">
              <span
                aria-hidden
                className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-brand-50 text-lg font-semibold text-brand-700 dark:bg-brand-500/15 dark:text-brand-300"
              >
                {initialsOf(profile)}
              </span>
              <div className="min-w-0">
                <p className="truncate text-base font-semibold text-fg">{profile.displayName}</p>
                <p className="truncate text-sm text-fg-muted">{profile.legalName}</p>
              </div>
            </div>

            <dl className="grid gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-0.5">
                <dt className="text-xs font-semibold uppercase tracking-wide text-fg-muted">
                  Mobile
                </dt>
                <dd className="nums text-sm text-fg">{profile.phone}</dd>
              </div>
              <div className="flex flex-col gap-0.5">
                <dt className="text-xs font-semibold uppercase tracking-wide text-fg-muted">
                  Email
                </dt>
                <dd className="break-all text-sm text-fg">
                  {profile.email ?? <span className="text-fg-subtle">Not set</span>}
                </dd>
              </div>
              <div className="flex flex-col gap-1">
                <dt className="text-xs font-semibold uppercase tracking-wide text-fg-muted">
                  Account status
                </dt>
                <dd>
                  <Badge tone={STATUS_TONES[profile.status]}>
                    {STATUS_LABELS[profile.status] ?? profile.status}
                  </Badge>
                </dd>
              </div>
            </dl>
          </>
        )}

        <OnboardingProgress status={profile.onboardingStatus} />
      </div>
    </Card>
  );
}

/** Up to two initials from the display name, for the avatar placeholder. */
function initialsOf(profile: ProviderProfile): string {
  const source = profile.displayName.trim() || profile.legalName.trim();
  const parts = source.split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  return (parts[0][0] + (parts.length > 1 ? parts[parts.length - 1][0] : "")).toUpperCase();
}

/**
 * Where the provider is in onboarding, as a track of steps. Steps at or before
 * the current one are marked done; the current one is highlighted. A step is
 * never rendered as a control — nothing here is clickable, because the work
 * that advances it happens in the sections below.
 */
function OnboardingProgress({ status }: { status: ProviderOnboardingStatus }) {
  const currentIndex = ONBOARDING_STEPS.findIndex((step) => step.value === status);
  const isComplete = status === "Completed";

  return (
    <div className="rounded-xl border border-line bg-surface-2 p-4">
      <div className="flex items-baseline justify-between gap-3">
        <p className="text-xs font-semibold uppercase tracking-wide text-fg-muted">Onboarding</p>
        <p className="nums text-xs text-fg-subtle">
          {Math.max(currentIndex + 1, 1)} of {ONBOARDING_STEPS.length}
        </p>
      </div>

      <ol className="mt-3 flex items-center gap-1.5">
        {ONBOARDING_STEPS.map((step, index) => {
          const done = currentIndex >= 0 && index <= currentIndex;
          return (
            <li key={step.value} className="flex min-w-0 flex-1 flex-col gap-1.5">
              <span
                aria-hidden
                className={cx(
                  "h-1.5 rounded-full transition-colors duration-fast ease-out",
                  done ? "bg-brand-600" : "bg-surface-3",
                )}
              />
              <span
                className={cx(
                  "truncate text-[0.6875rem] leading-tight",
                  index === currentIndex ? "font-semibold text-fg" : "text-fg-subtle",
                )}
              >
                {step.label}
              </span>
            </li>
          );
        })}
      </ol>

      <p className="mt-3 text-xs leading-relaxed text-fg-muted">
        {isComplete
          ? "You're fully onboarded and can be matched to jobs."
          : "Complete your KYC documents, service areas and skills below to start receiving jobs."}
      </p>
    </div>
  );
}
