"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import {
  AuthShell,
  OtpField,
  ResendRow,
  useResendCountdown,
} from "@/components/auth-ui";
import { Alert, Button, Checkbox, Field } from "@/components/ui";
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
  const [mobile, setMobile] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [resending, setResending] = useState(false);
  const { remaining, start, canResend } = useResendCountdown();

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
      // Must start false. Defaulting to true pre-ticks the consent box, so the
      // schema's "you must accept" rule can never fire and the account is
      // created recording a consent the customer never actually gave.
      consentAccepted: false as unknown as true,
    },
  });

  const sendCode = async (value: string) => {
    await apiFetch(`${API_V1}/auth/registration/otp`, {
      method: "POST",
      body: JSON.stringify({ mobile: value }),
    });
    start();
  };

  const onRequestOtp = otpForm.handleSubmit(async ({ mobile: value }) => {
    setError(null);
    try {
      await sendCode(value);
      setMobile(value);
      detailsForm.setValue("mobile", value);
      setNotice(`We sent a 6-digit code to ${value}.`);
      setStep("details");
    } catch (err) {
      setError(describeError(err));
    }
  });

  const onResend = async () => {
    setError(null);
    setResending(true);
    try {
      await sendCode(mobile);
      setNotice(`We sent a new code to ${mobile}.`);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setResending(false);
    }
  };

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
    <AuthShell
      title="Create your account"
      subtitle="We verify your mobile number with a one-time code."
      footer={
        <>
          Already have an account?{" "}
          <Link
            href="/login"
            className="font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
          >
            Sign in
          </Link>
        </>
      }
    >
      {step === "otp" ? (
        <form onSubmit={onRequestOtp} className="flex flex-col gap-4" noValidate>
          {error ? <Alert>{error}</Alert> : null}
          <Field
            label="Mobile number"
            type="tel"
            autoComplete="tel"
            placeholder="+919876543210"
            hint="We'll text you a 6-digit code to confirm it's you."
            error={otpForm.formState.errors.mobile?.message}
            {...otpForm.register("mobile")}
          />
          <Button type="submit" size="lg" fullWidth loading={otpForm.formState.isSubmitting}>
            Send code
          </Button>
        </form>
      ) : (
        <form onSubmit={onRegister} className="flex flex-col gap-4" noValidate>
          {error ? <Alert>{error}</Alert> : null}
          {notice ? <Alert tone="info">{notice}</Alert> : null}

          <OtpField
            error={detailsForm.formState.errors.otpCode?.message}
            {...detailsForm.register("otpCode")}
          />

          <ResendRow
            remaining={remaining}
            canResend={canResend}
            onResend={onResend}
            pending={resending}
          />

          <div className="my-1 border-t border-line" />

          <Field
            label="Full name"
            required
            autoComplete="name"
            error={detailsForm.formState.errors.name?.message}
            {...detailsForm.register("name")}
          />
          <Field
            label="Email"
            type="email"
            autoComplete="email"
            hint="Optional — needed only if you want to sign in with a password."
            error={detailsForm.formState.errors.email?.message}
            {...detailsForm.register("email")}
          />
          <Field
            label="Password"
            type="password"
            autoComplete="new-password"
            hint="Optional — at least 8 characters."
            error={detailsForm.formState.errors.password?.message}
            {...detailsForm.register("password")}
          />

          <div className="flex flex-col gap-1.5">
            <Checkbox
              label="I accept the Terms & Privacy Policy"
              {...detailsForm.register("consentAccepted")}
            />
            {detailsForm.formState.errors.consentAccepted ? (
              <p className="text-xs font-medium text-danger">
                {detailsForm.formState.errors.consentAccepted.message}
              </p>
            ) : null}
          </div>

          <Button type="submit" size="lg" fullWidth loading={detailsForm.formState.isSubmitting}>
            Create account
          </Button>

          <Button
            type="button"
            variant="ghost"
            onClick={() => {
              setStep("otp");
              setNotice(null);
              setError(null);
            }}
          >
            Use a different number ({mobile})
          </Button>
        </form>
      )}
    </AuthShell>
  );
}
