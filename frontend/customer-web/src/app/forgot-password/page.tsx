"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";

// Mirrors PasswordResetValidators.cs.
const requestSchema = z.object({ email: z.email("Enter a valid email address") });

const resetSchema = z.object({
  email: z.email("Enter a valid email address"),
  otpCode: z.string().regex(/^\d{6}$/, "Enter the 6-digit code"),
  newPassword: z.string().min(8, "Password must be at least 8 characters"),
});

export default function ForgotPasswordPage() {
  const router = useRouter();
  const [step, setStep] = useState<"request" | "reset">("request");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const requestForm = useForm<z.infer<typeof requestSchema>>({
    resolver: zodResolver(requestSchema),
    defaultValues: { email: "" },
  });

  const resetForm = useForm<z.infer<typeof resetSchema>>({
    resolver: zodResolver(resetSchema),
    defaultValues: { email: "", otpCode: "", newPassword: "" },
  });

  const onRequest = requestForm.handleSubmit(async ({ email }) => {
    setError(null);
    try {
      await apiFetch(`${API_V1}/auth/password/forgot`, {
        method: "POST",
        body: JSON.stringify({ email }),
      });
      resetForm.setValue("email", email);
      // Worded to match the server's behaviour: it deliberately does not
      // reveal whether the address is registered, so neither does this.
      setNotice(
        "If that email is registered, we sent a 6-digit code to the mobile number on the account.",
      );
      setStep("reset");
    } catch (err) {
      setError(describeError(err));
    }
  });

  const onReset = resetForm.handleSubmit(async (values) => {
    setError(null);
    try {
      await apiFetch(`${API_V1}/auth/password/reset`, {
        method: "POST",
        body: JSON.stringify(values),
      });
      router.push("/login");
    } catch (err) {
      setError(describeError(err));
    }
  });

  return (
    <main className="mx-auto w-full max-w-md px-6 py-12">
      <PageHeading
        title="Reset your password"
        subtitle="We verify the code against the mobile number on your account."
      />

      <Card title={step === "request" ? "Find your account" : "Choose a new password"}>
        <div className="flex flex-col gap-4">
          {error ? <Alert>{error}</Alert> : null}
          {step === "reset" && notice ? <Alert tone="info">{notice}</Alert> : null}

          {step === "request" ? (
            <form onSubmit={onRequest} className="flex flex-col gap-4" noValidate>
              <Field
                label="Email"
                type="email"
                autoComplete="email"
                error={requestForm.formState.errors.email?.message}
                {...requestForm.register("email")}
              />
              <Button type="submit" disabled={requestForm.formState.isSubmitting}>
                {requestForm.formState.isSubmitting ? "Sending…" : "Send reset code"}
              </Button>
            </form>
          ) : (
            <form onSubmit={onReset} className="flex flex-col gap-4" noValidate>
              <Field
                label="6-digit code"
                inputMode="numeric"
                autoComplete="one-time-code"
                maxLength={6}
                error={resetForm.formState.errors.otpCode?.message}
                {...resetForm.register("otpCode")}
              />
              <Field
                label="New password"
                type="password"
                autoComplete="new-password"
                error={resetForm.formState.errors.newPassword?.message}
                {...resetForm.register("newPassword")}
              />
              <p className="text-xs text-neutral-600 dark:text-neutral-400">
                Resetting your password signs you out everywhere else.
              </p>
              <Button type="submit" disabled={resetForm.formState.isSubmitting}>
                {resetForm.formState.isSubmitting ? "Saving…" : "Set new password"}
              </Button>
            </form>
          )}
        </div>
      </Card>

      <p className="mt-6 text-sm text-neutral-600 dark:text-neutral-400">
        <Link href="/login" className="underline">
          Back to sign in
        </Link>
      </p>
    </main>
  );
}
