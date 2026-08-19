"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { AuthShell, ResendRow, useResendCountdown } from "@/components/auth-ui";
import { OtpInput } from "@/components/OtpInput";
import { Alert, Button, Field } from "@/components/ui";
import { describeError, describeLoginError } from "@/lib/api";
import { requestLoginOtp, verifyLoginOtp } from "@/lib/auth-api";
import { isAuthenticated, storeSession, subscribeToAuthChanges } from "@/lib/auth";
import type { ProviderLoginResponse } from "@/lib/types";

// Dev-only test-auth backdoor (docs/DEVOPS.md "Dev-only provider test
// login"). NEXT_PUBLIC_ENABLE_DEV_AUTH is unset by default in every
// committed env file, so this button and the /api/dev-login route it calls
// are both dark in a normal checkout - it only appears when a developer
// opts in locally. The shared secret itself never reaches this bundle: it
// stays server-side in /api/dev-login (see that route for why).
const DEV_AUTH_ENABLED = process.env.NEXT_PUBLIC_ENABLE_DEV_AUTH === "true";

// Basic shape validation before spending a request; the server remains the
// authority on what counts as a valid mobile number.
const mobileSchema = z.object({
  mobile: z
    .string()
    .min(7, "Enter a valid mobile number")
    .max(15, "Enter a valid mobile number")
    .regex(/^[0-9+]+$/, "Digits only (a leading + is fine)"),
});
type MobileFormValues = z.infer<typeof mobileSchema>;

const otpSchema = z.object({
  otpCode: z
    .string()
    .min(4, "Enter the code you received")
    .max(8, "Enter the code you received")
    .regex(/^[0-9]+$/, "The code is numeric"),
});
type OtpFormValues = z.infer<typeof otpSchema>;

/**
 * Provider sign-in (docs/PROVIDER.md's OTP-based auth): mobile number entry
 * requests an OTP, then the provider enters that code to obtain a session.
 * Two independent forms rather than one, one per step - it keeps each
 * step's validation and submit handler simple instead of one form juggling
 * two very different "what does submit do here" meanings.
 *
 * This page is deliberately retained (task #206) even though customer-web's
 * unified /login can also authenticate a provider: a bookmarked or directly
 * typed provider-web origin must still be able to sign in. The direct
 * provider-api calls below are exactly what #206 specified, unchanged.
 */
export default function ProviderLoginPage() {
  const router = useRouter();
  const [step, setStep] = useState<"mobile" | "otp">("mobile");
  const [mobile, setMobile] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [infoMessage, setInfoMessage] = useState<string | null>(null);
  const [isResending, setIsResending] = useState(false);
  const resend = useResendCountdown();

  // Already signed in (e.g. back-button to /login with a live session) -
  // send straight to the jobs list instead of showing the form again.
  useEffect(() => {
    const sync = () => {
      if (isAuthenticated()) router.replace("/jobs");
    };
    sync();
    return subscribeToAuthChanges(sync);
  }, [router]);

  // Read the query string directly (rather than next/navigation's
  // useSearchParams) so this page can stay statically prerendered - a
  // useSearchParams call forces the whole page out of static rendering
  // unless wrapped in its own Suspense boundary, which is unnecessary
  // machinery for a single one-off banner.
  useEffect(() => {
    if (typeof window === "undefined") return;
    if (new URLSearchParams(window.location.search).get("registered") === "1") {
      setInfoMessage("Registration submitted. Enter your mobile number to sign in.");
    }
  }, []);

  const mobileForm = useForm<MobileFormValues>({
    resolver: zodResolver(mobileSchema),
    defaultValues: { mobile: "" },
  });

  const otpForm = useForm<OtpFormValues>({
    resolver: zodResolver(otpSchema),
    defaultValues: { otpCode: "" },
  });

  const requestOtp = mobileForm.handleSubmit(async (values) => {
    setError(null);
    try {
      await requestLoginOtp({ mobile: values.mobile });
      setMobile(values.mobile);
      setInfoMessage(`We sent a verification code to ${values.mobile}.`);
      setStep("otp");
      otpForm.reset({ otpCode: "" });
      resend.start();
    } catch (err) {
      setError(describeError(err));
    }
  });

  const verifyOtp = otpForm.handleSubmit(async (values) => {
    setError(null);
    try {
      const session = await verifyLoginOtp({ mobile, otpCode: values.otpCode });
      storeSession(session);
      router.push("/jobs");
    } catch (err) {
      setError(describeLoginError(err));
    }
  });

  // Resend is the same request as the first send - the countdown, not a
  // different endpoint, is what stops a provider burning their SMS quota.
  const resendOtp = async () => {
    setError(null);
    setIsResending(true);
    try {
      await requestLoginOtp({ mobile });
      setInfoMessage(`We sent a new verification code to ${mobile}.`);
      resend.start();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setIsResending(false);
    }
  };

  const changeNumber = () => {
    setStep("mobile");
    setError(null);
    setInfoMessage(null);
    otpForm.reset({ otpCode: "" });
  };

  const [devSigningIn, setDevSigningIn] = useState(false);
  const devSignIn = async () => {
    setError(null);
    setDevSigningIn(true);
    try {
      const response = await fetch("/api/dev-login", { method: "POST" });
      if (!response.ok) {
        throw new Error("Dev sign-in failed. Is provider-api running with DevAuth configured?");
      }
      const session = (await response.json()) as ProviderLoginResponse;
      storeSession(session);
      router.push("/jobs");
    } catch (err) {
      setError(describeError(err));
    } finally {
      setDevSigningIn(false);
    }
  };

  return (
    <AuthShell
      title="Provider sign in"
      subtitle={
        step === "mobile"
          ? "Sign in with your registered mobile number."
          : `Enter the 6-digit code we sent to ${mobile}.`
      }
      footer={
        <>
          New provider?{" "}
          <Link
            href="/register"
            className="font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
          >
            Register here
          </Link>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alert>{error}</Alert> : null}
        {infoMessage && !error ? <Alert tone="info">{infoMessage}</Alert> : null}

        {step === "mobile" ? (
          <form onSubmit={requestOtp} className="flex flex-col gap-4" noValidate>
            <Field
              label="Mobile number"
              type="tel"
              inputMode="tel"
              autoComplete="tel"
              autoFocus
              placeholder="+919876543210"
              error={mobileForm.formState.errors.mobile?.message}
              {...mobileForm.register("mobile")}
            />
            <Button type="submit" size="lg" fullWidth loading={mobileForm.formState.isSubmitting}>
              Send verification code
            </Button>
          </form>
        ) : (
          <form onSubmit={verifyOtp} className="flex flex-col gap-4" noValidate>
            <OtpInput
              autoFocus
              error={otpForm.formState.errors.otpCode?.message}
              {...otpForm.register("otpCode")}
            />
            <Button type="submit" size="lg" fullWidth loading={otpForm.formState.isSubmitting}>
              Sign in
            </Button>

            <ResendRow
              remaining={resend.remaining}
              canResend={resend.canResend}
              onResend={resendOtp}
              pending={isResending}
            />

            <Button type="button" variant="ghost" fullWidth onClick={changeNumber}>
              Use a different number
            </Button>
          </form>
        )}

        {DEV_AUTH_ENABLED ? (
          <Button
            type="button"
            variant="ghost"
            fullWidth
            loading={devSigningIn}
            onClick={devSignIn}
            className="border border-dashed border-amber-500 text-amber-600 dark:text-amber-400"
          >
            Dev sign in (test provider) — local only, skips OTP
          </Button>
        ) : null}
      </div>
    </AuthShell>
  );
}
