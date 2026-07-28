"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, Checkbox, Field, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { CommunicationPreferences, CustomerProfile } from "@/lib/types";

// Mirrors ProfileValidators.cs.
const profileSchema = z.object({
  name: z.string().min(1, "Name is required").max(200),
  dateOfBirth: z.string(),
  city: z.string().max(200),
  state: z.string().max(200),
  pincode: z.union([z.string().regex(/^\d{6}$/, "Pincode must be 6 digits"), z.literal("")]),
  country: z.string().max(200),
});

const mobileChangeSchema = z.object({
  newMobile: z.string().regex(/^\+?[1-9]\d{7,14}$/, "Enter a valid mobile number"),
  otpCode: z.string().regex(/^\d{6}$/, "Enter the 6-digit code"),
});

const emailChangeSchema = z.object({
  newEmail: z.email("Enter a valid email address"),
  otpCode: z.string().regex(/^\d{6}$/, "Enter the 6-digit code"),
});

export default function ProfilePage() {
  return (
    <RequireAuth>
      <ProfileScreen />
    </RequireAuth>
  );
}

function ProfileScreen() {
  const profileQuery = useQuery({
    queryKey: ["profile"],
    queryFn: () => apiFetch<CustomerProfile>(`${API_V1}/profile`, { authenticated: true }),
  });

  return (
    <main className="mx-auto w-full max-w-2xl px-6 py-12">
      <PageHeading title="Your profile" subtitle="View and edit your details and preferences." />

      {profileQuery.isPending ? (
        <p className="text-sm text-neutral-500">Loading your profile…</p>
      ) : profileQuery.isError ? (
        <Alert>{describeError(profileQuery.error)}</Alert>
      ) : (
        <div className="flex flex-col gap-6">
          <ProfileDetails profile={profileQuery.data} />
          <ChangeMobile currentMobile={profileQuery.data.mobile} />
          <ChangeEmail currentEmail={profileQuery.data.email} />
          <PreferencesForm />
        </div>
      )}
    </main>
  );
}

function ProfileDetails({ profile }: { profile: CustomerProfile }) {
  const queryClient = useQueryClient();
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const form = useForm<z.infer<typeof profileSchema>>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      name: profile.name,
      // The API sends a full ISO timestamp; <input type="date"> needs only
      // the date part.
      dateOfBirth: profile.dateOfBirth ? profile.dateOfBirth.slice(0, 10) : "",
      city: profile.city ?? "",
      state: profile.state ?? "",
      pincode: profile.pincode ?? "",
      country: profile.country ?? "",
    },
  });

  const mutation = useMutation({
    mutationFn: (values: z.infer<typeof profileSchema>) =>
      apiFetch<CustomerProfile>(`${API_V1}/profile`, {
        method: "PUT",
        authenticated: true,
        body: JSON.stringify({
          name: values.name,
          dateOfBirth: values.dateOfBirth === "" ? null : values.dateOfBirth,
          city: values.city === "" ? null : values.city,
          state: values.state === "" ? null : values.state,
          pincode: values.pincode === "" ? null : values.pincode,
          country: values.country === "" ? null : values.country,
        }),
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["profile"], updated);
      setSaved(true);
      setError(null);
    },
    onError: (err) => {
      setSaved(false);
      setError(describeError(err));
    },
  });

  return (
    <Card
      title="Details"
      description="Your mobile number and email are changed separately, with verification."
    >
      <form
        onSubmit={form.handleSubmit((values) => mutation.mutate(values))}
        className="flex flex-col gap-4"
        noValidate
      >
        {error ? <Alert>{error}</Alert> : null}
        {saved ? <Alert tone="success">Profile saved.</Alert> : null}

        <Field label="Mobile number" value={profile.mobile} readOnly disabled />
        <Field label="Email" value={profile.email ?? "Not set"} readOnly disabled />
        <Field
          label="Full name"
          autoComplete="name"
          error={form.formState.errors.name?.message}
          {...form.register("name")}
        />
        <Field
          label="Date of birth"
          type="date"
          error={form.formState.errors.dateOfBirth?.message}
          {...form.register("dateOfBirth")}
        />
        <Field label="City" error={form.formState.errors.city?.message} {...form.register("city")} />
        <Field label="State" error={form.formState.errors.state?.message} {...form.register("state")} />
        <Field
          label="Pincode"
          inputMode="numeric"
          maxLength={6}
          error={form.formState.errors.pincode?.message}
          {...form.register("pincode")}
        />
        <Field
          label="Country"
          error={form.formState.errors.country?.message}
          {...form.register("country")}
        />
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? "Saving…" : "Save changes"}
        </Button>
      </form>
    </Card>
  );
}

function ChangeMobile({ currentMobile }: { currentMobile: string }) {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [codeSent, setCodeSent] = useState(false);

  const form = useForm<z.infer<typeof mobileChangeSchema>>({
    resolver: zodResolver(mobileChangeSchema),
    defaultValues: { newMobile: "", otpCode: "" },
  });

  const requestOtp = async () => {
    setError(null);
    const newMobile = form.getValues("newMobile");
    const parsed = mobileChangeSchema.shape.newMobile.safeParse(newMobile);
    if (!parsed.success) {
      form.setError("newMobile", { message: parsed.error.issues[0].message });
      return;
    }

    try {
      await apiFetch(`${API_V1}/profile/mobile/otp`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ newMobile }),
      });
      setCodeSent(true);
      setNotice(`We sent a code to ${newMobile}. Enter it below to confirm the change.`);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const confirm = form.handleSubmit(async (values) => {
    setError(null);
    try {
      const updated = await apiFetch<CustomerProfile>(`${API_V1}/profile/mobile`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify(values),
      });
      queryClient.setQueryData(["profile"], updated);
      form.reset({ newMobile: "", otpCode: "" });
      setCodeSent(false);
      setNotice("Mobile number updated.");
    } catch (err) {
      setError(describeError(err));
    }
  });

  return (
    <Card title="Change mobile number" description={`Currently ${currentMobile}.`}>
      <form onSubmit={confirm} className="flex flex-col gap-4" noValidate>
        {error ? <Alert>{error}</Alert> : null}
        {notice ? <Alert tone="info">{notice}</Alert> : null}

        <Field
          label="New mobile number"
          type="tel"
          placeholder="+919876543210"
          error={form.formState.errors.newMobile?.message}
          {...form.register("newMobile")}
        />

        {codeSent ? (
          <>
            <Field
              label="6-digit code"
              inputMode="numeric"
              maxLength={6}
              error={form.formState.errors.otpCode?.message}
              {...form.register("otpCode")}
            />
            <Button type="submit" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting ? "Confirming…" : "Confirm change"}
            </Button>
          </>
        ) : (
          <Button type="button" variant="secondary" onClick={requestOtp}>
            Send verification code
          </Button>
        )}
      </form>
    </Card>
  );
}

function ChangeEmail({ currentEmail }: { currentEmail: string | null }) {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [codeSent, setCodeSent] = useState(false);

  const form = useForm<z.infer<typeof emailChangeSchema>>({
    resolver: zodResolver(emailChangeSchema),
    defaultValues: { newEmail: "", otpCode: "" },
  });

  const requestOtp = async () => {
    setError(null);
    const newEmail = form.getValues("newEmail");
    const parsed = emailChangeSchema.shape.newEmail.safeParse(newEmail);
    if (!parsed.success) {
      form.setError("newEmail", { message: parsed.error.issues[0].message });
      return;
    }

    try {
      await apiFetch(`${API_V1}/profile/email/otp`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ newEmail }),
      });
      setCodeSent(true);
      setNotice(`We sent a code to ${newEmail}. Enter it below to confirm the change.`);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const confirm = form.handleSubmit(async (values) => {
    setError(null);
    try {
      const updated = await apiFetch<CustomerProfile>(`${API_V1}/profile/email`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify(values),
      });
      queryClient.setQueryData(["profile"], updated);
      form.reset({ newEmail: "", otpCode: "" });
      setCodeSent(false);
      setNotice("Email address updated.");
    } catch (err) {
      setError(describeError(err));
    }
  });

  return (
    <Card
      title="Change email address"
      description={currentEmail ? `Currently ${currentEmail}.` : "No email address set."}
    >
      <form onSubmit={confirm} className="flex flex-col gap-4" noValidate>
        {error ? <Alert>{error}</Alert> : null}
        {notice ? <Alert tone="info">{notice}</Alert> : null}

        <Field
          label="New email address"
          type="email"
          error={form.formState.errors.newEmail?.message}
          {...form.register("newEmail")}
        />

        {codeSent ? (
          <>
            <Field
              label="6-digit code"
              inputMode="numeric"
              maxLength={6}
              error={form.formState.errors.otpCode?.message}
              {...form.register("otpCode")}
            />
            <Button type="submit" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting ? "Confirming…" : "Confirm change"}
            </Button>
          </>
        ) : (
          <Button type="button" variant="secondary" onClick={requestOtp}>
            Send verification code
          </Button>
        )}
      </form>
    </Card>
  );
}

const PREFERENCE_FIELDS = [
  { key: "transactionalSms", label: "Booking and account updates by SMS" },
  { key: "transactionalEmail", label: "Booking and account updates by email" },
  { key: "transactionalWhatsApp", label: "Booking and account updates on WhatsApp" },
  { key: "promotionalSms", label: "Offers and promotions by SMS" },
  { key: "promotionalEmail", label: "Offers and promotions by email" },
  { key: "promotionalWhatsApp", label: "Offers and promotions on WhatsApp" },
  { key: "push", label: "Push notifications" },
] as const;

type PreferenceKey = (typeof PREFERENCE_FIELDS)[number]["key"];
type PreferenceState = Record<PreferenceKey, boolean>;

function PreferencesForm() {
  const queryClient = useQueryClient();
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["preferences"],
    queryFn: () =>
      apiFetch<CommunicationPreferences>(`${API_V1}/profile/preferences`, {
        authenticated: true,
      }),
  });

  const [state, setState] = useState<PreferenceState | null>(null);

  // Seeded from the server response rather than held only in the query cache:
  // the checkboxes are edited locally and saved as one batch.
  useEffect(() => {
    if (!query.data) return;
    setState(
      Object.fromEntries(
        PREFERENCE_FIELDS.map(({ key }) => [key, query.data[key]]),
      ) as PreferenceState,
    );
  }, [query.data]);

  const mutation = useMutation({
    mutationFn: (values: PreferenceState) =>
      apiFetch<CommunicationPreferences>(`${API_V1}/profile/preferences`, {
        method: "PUT",
        authenticated: true,
        body: JSON.stringify(values),
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["preferences"], updated);
      setSaved(true);
      setError(null);
    },
    onError: (err) => {
      setSaved(false);
      setError(describeError(err));
    },
  });

  if (query.isPending || !state) {
    return (
      <Card title="Communication preferences">
        <p className="text-sm text-neutral-500">Loading preferences…</p>
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Communication preferences">
        <Alert>{describeError(query.error)}</Alert>
      </Card>
    );
  }

  return (
    <Card
      title="Communication preferences"
      description="Security codes are always sent, and are not affected by these settings."
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          mutation.mutate(state);
        }}
        className="flex flex-col gap-3"
      >
        {error ? <Alert>{error}</Alert> : null}
        {saved ? <Alert tone="success">Preferences saved.</Alert> : null}

        {PREFERENCE_FIELDS.map(({ key, label }) => (
          <Checkbox
            key={key}
            name={key}
            label={label}
            checked={state[key]}
            onChange={(event) => {
              setSaved(false);
              setState({ ...state, [key]: event.target.checked });
            }}
          />
        ))}

        <Button type="submit" className="mt-2 self-start" disabled={mutation.isPending}>
          {mutation.isPending ? "Saving…" : "Save preferences"}
        </Button>
      </form>
    </Card>
  );
}
