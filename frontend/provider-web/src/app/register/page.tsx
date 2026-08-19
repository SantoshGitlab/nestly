"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { AuthShell, ResendRow, useResendCountdown } from "@/components/auth-ui";
import { OtpInput } from "@/components/OtpInput";
import { Alert, Button, CheckboxField, Field } from "@/components/ui";
import { describeError } from "@/lib/api";
import { registerProvider, requestRegistrationOtp } from "@/lib/auth-api";

const mobileSchema = z.object({
  mobile: z
    .string()
    .min(7, "Enter a valid mobile number")
    .max(15, "Enter a valid mobile number")
    .regex(/^[0-9+]+$/, "Digits only (a leading + is fine)"),
});
type MobileFormValues = z.infer<typeof mobileSchema>;

const detailsSchema = z.object({
  otpCode: z
    .string()
    .min(4, "Enter the code you received")
    .max(8, "Enter the code you received")
    .regex(/^[0-9]+$/, "The code is numeric"),
  legalName: z.string().min(1, "Legal name is required").max(200),
  displayName: z.string().min(1, "Display name is required").max(100),
  email: z.union([z.email("Enter a valid email address"), z.literal("")]),
  consentAccepted: z.literal(true, {
    error: "You must accept the terms to register.",
  }),
});
type DetailsFormValues = z.infer<typeof detailsSchema>;

/**
 * Provider registration (docs/PROVIDER.md's OTP-based auth): mobile number
 * entry requests an OTP, then the provider supplies their details alongside
 * that code in a single submission (matching the API contract - registration
 * itself does not return a session, so a successful registration sends the
 * provider to /login to sign in with a fresh OTP).
 */
export default function ProviderRegisterPage() {
  const router = useRouter();
  const [step, setStep] = useState<"mobile" | "details">("mobile");
  const [mobile, setMobile] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [infoMessage, setInfoMessage] = useState<string | null>(null);
  const [isResending, setIsResending] = useState(false);
  const resend = useResendCountdown();

  const mobileForm = useForm<MobileFormValues>({
    resolver: zodResolver(mobileSchema),
    defaultValues: { mobile: "" },
  });

  const detailsForm = useForm<DetailsFormValues>({
    resolver: zodResolver(detailsSchema),
    defaultValues: {
      otpCode: "",
      legalName: "",
      displayName: "",
      email: "",
      // Deliberately false: pre-ticking a consent box records an agreement the
      // provider never gave, and would make the schema's z.literal(true) rule
      // unreachable.
      consentAccepted: false as unknown as true,
    },
  });

  const requestOtp = mobileForm.handleSubmit(async (values) => {
    setError(null);
    try {
      await requestRegistrationOtp({ mobile: values.mobile });
      setMobile(values.mobile);
      setInfoMessage(`We sent a verification code to ${values.mobile}.`);
      setStep("details");
      resend.start();
    } catch (err) {
      setError(describeError(err));
    }
  });

  const submitRegistration = detailsForm.handleSubmit(async (values) => {
    setError(null);
    try {
      await registerProvider({
        mobile,
        otpCode: values.otpCode,
        legalName: values.legalName,
        displayName: values.displayName,
        email: values.email === "" ? undefined : values.email,
        consentAccepted: values.consentAccepted,
      });
      router.push("/login?registered=1");
    } catch (err) {
      setError(describeError(err));
    }
  });

  // Same endpoint as the first send; the countdown is what prevents a provider
  // burning their SMS quota and tripping gateway rate limiting.
  const resendOtp = async () => {
    setError(null);
    setIsResending(true);
    try {
      await requestRegistrationOtp({ mobile });
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
    detailsForm.reset();
  };

  const consentError = detailsForm.formState.errors.consentAccepted?.message;

  return (
    <AuthShell
      title="Become a Nestly provider"
      subtitle={
        step === "mobile"
          ? "Register with your mobile number to start onboarding."
          : "Verify your number and tell us who you are."
      }
      footer={
        <>
          Already registered?{" "}
          <Link
            href="/login"
            className="font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
          >
            Sign in
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
          <form onSubmit={submitRegistration} className="flex flex-col gap-5" noValidate>
            <OtpInput
              autoFocus
              error={detailsForm.formState.errors.otpCode?.message}
              {...detailsForm.register("otpCode")}
            />

            <ResendRow
              remaining={resend.remaining}
              canResend={resend.canResend}
              onResend={resendOtp}
              pending={isResending}
            />

            <div className="flex flex-col gap-4 border-t border-line pt-5">
              <Field
                label="Legal name"
                hint="As it appears on your identity documents."
                autoComplete="name"
                error={detailsForm.formState.errors.legalName?.message}
                {...detailsForm.register("legalName")}
              />
              <Field
                label="Display name"
                hint="What customers will see."
                error={detailsForm.formState.errors.displayName?.message}
                {...detailsForm.register("displayName")}
              />
              <Field
                label="Email (optional)"
                type="email"
                inputMode="email"
                autoComplete="email"
                error={detailsForm.formState.errors.email?.message}
                {...detailsForm.register("email")}
              />

              <div className="flex flex-col gap-1.5">
                <CheckboxField
                  label="I accept the provider terms and conditions"
                  description="Covers how jobs are assigned, how you are paid, and how your data is used."
                  checked={detailsForm.watch("consentAccepted") === true}
                  onChange={(checked) =>
                    detailsForm.setValue("consentAccepted", checked as unknown as true, {
                      shouldValidate: true,
                    })
                  }
                />
                {consentError ? (
                  <p className="text-xs font-medium text-danger">{consentError}</p>
                ) : null}
              </div>
            </div>

            <Button type="submit" size="lg" fullWidth loading={detailsForm.formState.isSubmitting}>
              Complete registration
            </Button>
            <Button type="button" variant="ghost" fullWidth onClick={changeNumber}>
              Use a different number
            </Button>
          </form>
        )}
      </div>
    </AuthShell>
  );
}
