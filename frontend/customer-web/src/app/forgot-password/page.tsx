"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { AuthShell, OtpField, ResendRow, useResendCountdown } from "@/components/auth-ui";
import { Alert, Button, Field } from "@/components/ui";
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
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [resending, setResending] = useState(false);
  const { remaining, start, canResend } = useResendCountdown();

  const requestForm = useForm<z.infer<typeof requestSchema>>({
    resolver: zodResolver(requestSchema),
    defaultValues: { email: "" },
  });

  const resetForm = useForm<z.infer<typeof resetSchema>>({
    resolver: zodResolver(resetSchema),
    defaultValues: { email: "", otpCode: "", newPassword: "" },
  });

  const sendCode = async (value: string) => {
    await apiFetch(`${API_V1}/auth/password/forgot`, {
      method: "POST",
      body: JSON.stringify({ email: value }),
    });
    start();
  };

  const onRequest = requestForm.handleSubmit(async ({ email: value }) => {
    setError(null);
    try {
      await sendCode(value);
      setEmail(value);
      resetForm.setValue("email", value);
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

  const onResend = async () => {
    setError(null);
    setResending(true);
    try {
      await sendCode(email);
      setNotice("If that email is registered, we sent a new code.");
    } catch (err) {
      setError(describeError(err));
    } finally {
      setResending(false);
    }
  };

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
    <AuthShell
      title="Reset your password"
      subtitle="We verify the code against the mobile number on your account."
      footer={
        <Link
          href="/login"
          className="font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
        >
          Back to sign in
        </Link>
      }
    >
      {step === "request" ? (
        <form onSubmit={onRequest} className="flex flex-col gap-4" noValidate>
          {error ? <Alert>{error}</Alert> : null}
          <Field
            label="Email"
            type="email"
            autoComplete="email"
            hint="The email address on your Nestly account."
            error={requestForm.formState.errors.email?.message}
            {...requestForm.register("email")}
          />
          <Button type="submit" size="lg" fullWidth loading={requestForm.formState.isSubmitting}>
            Send reset code
          </Button>
        </form>
      ) : (
        <form onSubmit={onReset} className="flex flex-col gap-4" noValidate>
          {error ? <Alert>{error}</Alert> : null}
          {notice ? <Alert tone="info">{notice}</Alert> : null}

          <OtpField
            error={resetForm.formState.errors.otpCode?.message}
            {...resetForm.register("otpCode")}
          />

          <ResendRow
            remaining={remaining}
            canResend={canResend}
            onResend={onResend}
            pending={resending}
          />

          <div className="my-1 border-t border-line" />

          <Field
            label="New password"
            type="password"
            required
            autoComplete="new-password"
            hint="At least 8 characters."
            error={resetForm.formState.errors.newPassword?.message}
            {...resetForm.register("newPassword")}
          />

          <Alert tone="warning">Resetting your password signs you out everywhere else.</Alert>

          <Button type="submit" size="lg" fullWidth loading={resetForm.formState.isSubmitting}>
            Set new password
          </Button>
        </form>
      )}
    </AuthShell>
  );
}
