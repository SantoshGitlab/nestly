"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import {
  AuthShell,
  ResendRow,
  useResendCountdown,
} from "@/components/auth-ui";
import { OtpInput } from "@/components/OtpInput";
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
    referralCode: z.string(),
  })
  // The server rejects a password without an email
  // (Registration.EmailRequiredForPassword); say so before the round trip.
  .refine((v) => v.password === "" || v.email !== "", {
    path: ["email"],
    message: "Email is required when you set a password",
  });

export default function RegisterPage() {
  // Suspense for useSearchParams below (see login/page.tsx for the same
  // pattern): reading the `?ref=` invite code opts this tree out of static
  // rendering, which the App Router requires a boundary around.
  return (
    <Suspense
      fallback={
        <AuthShell title="Create your account" subtitle="We verify your mobile number with a one-time code.">
          <div />
        </AuthShell>
      }
    >
      <RegisterScreen />
    </Suspense>
  );
}

function RegisterScreen() {
  const router = useRouter();
  // Shared referral links carry the referrer's code as `?ref=` (see
  // ReferralOptions.ShareLinkBaseUrl, "https://nestly.app/register?ref=").
  const referralCodeFromLink = useSearchParams().get("ref") ?? "";
  const [step, setStep] = useState<"otp" | "details">("otp");
  const [mobile, setMobile] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [resending, setResending] = useState(false);
  const [registered, setRegistered] = useState(false);
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
      referralCode: referralCodeFromLink,
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
          referralCode: values.referralCode === "" ? null : values.referralCode,
        }),
      });
      // A referral code was submitted: pause on a confirmation instead of an
      // immediate redirect. The server processes referrals best-effort
      // (CustomerRegistrationService.TryCreateReferralAsync) and never
      // reports back whether the code matched, so this can only confirm the
      // code was submitted, not that a reward was created.
      if (values.referralCode !== "") {
        setRegistered(true);
      } else {
        router.push("/login");
      }
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
      {registered ? (
        <div className="flex flex-col gap-4">
          <Alert tone="success">
            Account created — you were invited by a friend. If your code was
            valid, their reward will be added once it qualifies.
          </Alert>
          <Button size="lg" fullWidth onClick={() => router.push("/login")}>
            Continue to sign in
          </Button>
        </div>
      ) : step === "otp" ? (
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

          <OtpInput
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

          <Field
            label="Referral code"
            autoComplete="off"
            hint="Optional — from a friend's invite link."
            error={detailsForm.formState.errors.referralCode?.message}
            {...detailsForm.register("referralCode")}
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
