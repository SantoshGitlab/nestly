"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Checkbox, Field, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { CustomerSummary } from "@/lib/types";

// Mirrors RegistrationValidators.cs.
const mobileSchema = z
  .string()
  .regex(/^\+?[1-9]\d{7,14}$/, "Enter a valid mobile number");

const otpRequestSchema = z.object({ mobile: mobileSchema });

const registerSchema = z
  .object({
    mobile: mobileSchema,
    otpCode: z.string().regex(/^\d{6}$/, "Enter the 6-digit code"),
    name: z.string().min(1, "Name is required").max(200),
    email: z.union([z.email("Enter a valid email address"), z.literal("")]),
    password: z.union([
      z.string().min(8, "Password must be at least 8 characters"),
      z.literal(""),
    ]),
    consentAccepted: z.literal(true, {
      message: "You must accept the Terms & Privacy Policy",
    }),
  })
  // The server rejects a password without an email
  // (Registration.EmailRequiredForPassword); say so before the round trip.
  .refine((v) => v.password === "" || v.email !== "", {
    path: ["email"],
    message: "Email is required when you set a password",
  });

export default function RegisterPage() {
  const router = useRouter();
  const [step, setStep] = useState<"otp" | "details">("otp");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const otpForm = useForm<z.infer<typeof otpRequestSchema>>({
    resolver: zodResolver(otpRequestSchema),
    defaultValues: { mobile: "" },
  });

  const detailsForm = useForm<z.infer<typeof registerSchema>>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      mobile: "",
      otpCode: "",
      name: "",
      email: "",
      password: "",
      consentAccepted: true,
    },
  });

  const onRequestOtp = otpForm.handleSubmit(async ({ mobile }) => {
    setError(null);
    try {
      await apiFetch(`${API_V1}/auth/registration/otp`, {
        method: "POST",
        body: JSON.stringify({ mobile }),
      });
      detailsForm.setValue("mobile", mobile);
      setNotice(`We sent a 6-digit code to ${mobile}.`);
      setStep("details");
    } catch (err) {
      setError(describeError(err));
    }
  });

  const onRegister = detailsForm.handleSubmit(async (values) => {
    setError(null);
    try {
      await apiFetch<CustomerSummary>(`${API_V1}/auth/registration`, {
        method: "POST",
        body: JSON.stringify({
          mobile: values.mobile,
          otpCode: values.otpCode,
          name: values.name,
          // Empty strings would fail the server's EmailAddress rule; the
          // fields are genuinely optional, so send null instead.
          email: values.email === "" ? null : values.email,
          password: values.password === "" ? null : values.password,
          consentAccepted: values.consentAccepted,
        }),
      });
      router.push("/login");
    } catch (err) {
      setError(describeError(err));
    }
  });

  return (
    <main className="mx-auto w-full max-w-md px-6 py-12">
      <PageHeading
        title="Create your account"
        subtitle="We verify your mobile number with a one-time code."
      />

      <Card title={step === "otp" ? "Verify your mobile" : "Your details"}>
        <div className="flex flex-col gap-4">
          {error ? <Alert>{error}</Alert> : null}
          {step === "details" && notice ? <Alert tone="info">{notice}</Alert> : null}

          {step === "otp" ? (
            <form onSubmit={onRequestOtp} className="flex flex-col gap-4" noValidate>
              <Field
                label="Mobile number"
                type="tel"
                autoComplete="tel"
                placeholder="+919876543210"
                error={otpForm.formState.errors.mobile?.message}
                {...otpForm.register("mobile")}
              />
              <Button type="submit" disabled={otpForm.formState.isSubmitting}>
                {otpForm.formState.isSubmitting ? "Sending…" : "Send code"}
              </Button>
            </form>
          ) : (
            <form onSubmit={onRegister} className="flex flex-col gap-4" noValidate>
              <Field
                label="6-digit code"
                inputMode="numeric"
                autoComplete="one-time-code"
                maxLength={6}
                error={detailsForm.formState.errors.otpCode?.message}
                {...detailsForm.register("otpCode")}
              />
              <Field
                label="Full name"
                autoComplete="name"
                error={detailsForm.formState.errors.name?.message}
                {...detailsForm.register("name")}
              />
              <Field
                label="Email (optional)"
                type="email"
                autoComplete="email"
                error={detailsForm.formState.errors.email?.message}
                {...detailsForm.register("email")}
              />
              <Field
                label="Password (optional)"
                type="password"
                autoComplete="new-password"
                error={detailsForm.formState.errors.password?.message}
                {...detailsForm.register("password")}
              />
              <Checkbox
                label="I accept the Terms & Privacy Policy"
                {...detailsForm.register("consentAccepted")}
              />
              {detailsForm.formState.errors.consentAccepted ? (
                <p className="text-xs text-red-600 dark:text-red-400">
                  {detailsForm.formState.errors.consentAccepted.message}
                </p>
              ) : null}
              <Button type="submit" disabled={detailsForm.formState.isSubmitting}>
                {detailsForm.formState.isSubmitting ? "Creating…" : "Create account"}
              </Button>
            </form>
          )}
        </div>
      </Card>

      <p className="mt-6 text-sm text-neutral-600 dark:text-neutral-400">
        Already have an account?{" "}
        <Link href="/login" className="underline">
          Sign in
        </Link>
      </p>
    </main>
  );
}
