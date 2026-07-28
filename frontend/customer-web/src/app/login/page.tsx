"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { storeSession } from "@/lib/auth";
import type { LoginResponse } from "@/lib/types";

// Mirrors the server-side FluentValidation rules (LoginValidators.cs) so the
// common mistakes are caught before a request is spent. The server remains
// the authority — this is convenience, not trust.
const mobileSchema = z
  .string()
  .regex(/^\+?[1-9]\d{7,14}$/, "Enter a valid mobile number");

const otpRequestSchema = z.object({ mobile: mobileSchema });
const otpVerifySchema = z.object({
  mobile: mobileSchema,
  otpCode: z.string().regex(/^\d{6}$/, "Enter the 6-digit code"),
});
const passwordSchema = z.object({
  email: z.email("Enter a valid email address"),
  password: z.string().min(1, "Password is required"),
});

type Mode = "otp" | "password";

export default function LoginPage() {
  const [mode, setMode] = useState<Mode>("otp");

  return (
    <main className="mx-auto w-full max-w-md px-6 py-12">
      <PageHeading title="Sign in" subtitle="Use your mobile number, or your email and password." />

      <div className="mb-5 flex gap-2" role="tablist" aria-label="Sign-in method">
        <Button
          role="tab"
          aria-selected={mode === "otp"}
          variant={mode === "otp" ? "primary" : "secondary"}
          onClick={() => setMode("otp")}
        >
          Mobile OTP
        </Button>
        <Button
          role="tab"
          aria-selected={mode === "password"}
          variant={mode === "password" ? "primary" : "secondary"}
          onClick={() => setMode("password")}
        >
          Email &amp; password
        </Button>
      </div>

      {mode === "otp" ? <OtpLogin /> : <PasswordLogin />}

      <p className="mt-6 text-sm text-neutral-600 dark:text-neutral-400">
        New to Nestly?{" "}
        <Link href="/register" className="underline">
          Create an account
        </Link>
      </p>
    </main>
  );
}

function OtpLogin() {
  const router = useRouter();
  const [step, setStep] = useState<"request" | "verify">("request");
  const [mobile, setMobile] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const requestForm = useForm<z.infer<typeof otpRequestSchema>>({
    resolver: zodResolver(otpRequestSchema),
    defaultValues: { mobile: "" },
  });

  const verifyForm = useForm<z.infer<typeof otpVerifySchema>>({
    resolver: zodResolver(otpVerifySchema),
    defaultValues: { mobile: "", otpCode: "" },
  });

  const onRequest = requestForm.handleSubmit(async ({ mobile: value }) => {
    setError(null);
    try {
      await apiFetch(`${API_V1}/auth/login/otp`, {
        method: "POST",
        body: JSON.stringify({ mobile: value }),
      });
      setMobile(value);
      verifyForm.setValue("mobile", value);
      setNotice(`We sent a 6-digit code to ${value}.`);
      setStep("verify");
    } catch (err) {
      setError(describeError(err));
    }
  });

  const onVerify = verifyForm.handleSubmit(async (values) => {
    setError(null);
    try {
      const session = await apiFetch<LoginResponse>(`${API_V1}/auth/login/otp/verify`, {
        method: "POST",
        body: JSON.stringify(values),
      });
      storeSession(session);
      router.push("/profile");
    } catch (err) {
      setError(describeError(err));
    }
  });

  return (
    <Card title={step === "request" ? "Sign in with OTP" : "Enter your code"}>
      <div className="flex flex-col gap-4">
        {error ? <Alert>{error}</Alert> : null}
        {step === "verify" && notice ? <Alert tone="info">{notice}</Alert> : null}

        {step === "request" ? (
          <form onSubmit={onRequest} className="flex flex-col gap-4" noValidate>
            <Field
              label="Mobile number"
              type="tel"
              autoComplete="tel"
              placeholder="+919876543210"
              error={requestForm.formState.errors.mobile?.message}
              {...requestForm.register("mobile")}
            />
            <Button type="submit" disabled={requestForm.formState.isSubmitting}>
              {requestForm.formState.isSubmitting ? "Sending…" : "Send code"}
            </Button>
          </form>
        ) : (
          <form onSubmit={onVerify} className="flex flex-col gap-4" noValidate>
            <Field
              label="6-digit code"
              inputMode="numeric"
              autoComplete="one-time-code"
              maxLength={6}
              error={verifyForm.formState.errors.otpCode?.message}
              {...verifyForm.register("otpCode")}
            />
            <Button type="submit" disabled={verifyForm.formState.isSubmitting}>
              {verifyForm.formState.isSubmitting ? "Verifying…" : "Verify and sign in"}
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => {
                setStep("request");
                setNotice(null);
                setError(null);
              }}
            >
              Use a different number ({mobile})
            </Button>
          </form>
        )}
      </div>
    </Card>
  );
}

function PasswordLogin() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);

  const form = useForm<z.infer<typeof passwordSchema>>({
    resolver: zodResolver(passwordSchema),
    defaultValues: { email: "", password: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setError(null);
    try {
      const session = await apiFetch<LoginResponse>(`${API_V1}/auth/login/password`, {
        method: "POST",
        body: JSON.stringify(values),
      });
      storeSession(session);
      router.push("/profile");
    } catch (err) {
      setError(describeError(err));
    }
  });

  return (
    <Card title="Sign in with password">
      <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
        {error ? <Alert>{error}</Alert> : null}
        <Field
          label="Email"
          type="email"
          autoComplete="email"
          error={form.formState.errors.email?.message}
          {...form.register("email")}
        />
        <Field
          label="Password"
          type="password"
          autoComplete="current-password"
          error={form.formState.errors.password?.message}
          {...form.register("password")}
        />
        <Button type="submit" disabled={form.formState.isSubmitting}>
          {form.formState.isSubmitting ? "Signing in…" : "Sign in"}
        </Button>
        <Link href="/forgot-password" className="text-sm underline">
          Forgot your password?
        </Link>
      </form>
    </Card>
  );
}
