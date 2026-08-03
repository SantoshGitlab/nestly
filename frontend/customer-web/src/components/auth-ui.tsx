"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { cx } from "@/components/ui";

/**
 * Shared furniture for the authentication screens (login, register, OTP,
 * forgot-password).
 *
 * These screens used to be bare stacked form fields on a white page, which is
 * the first thing a new customer ever sees. They now share one frame so the
 * three read as a single flow, and the OTP step gets a real input affordance
 * instead of a plain text box.
 */

/** Centered auth frame: brand mark, heading, card, and an optional footer link. */
export function AuthShell({
  title,
  subtitle,
  children,
  footer,
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
  footer?: ReactNode;
}) {
  return (
    <main className="relative isolate flex min-h-[calc(100vh-4rem)] items-center justify-center overflow-hidden px-4 py-12">
      {/* Decorative brand wash. Sits behind everything and is inert to AT. */}
      <div
        aria-hidden
        className="absolute -top-40 left-1/2 -z-10 h-[28rem] w-[28rem] -translate-x-1/2 rounded-full bg-brand-500/10 blur-3xl"
      />

      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <Link href="/" className="inline-flex items-center gap-2" aria-label="Nestly home">
            <span
              aria-hidden
              className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-gradient text-fg-on-brand shadow-brand"
            >
              <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5">
                <path
                  d="M4 11.5 12 5l8 6.5V19a1 1 0 0 1-1 1h-4v-5h-6v5H5a1 1 0 0 1-1-1v-7.5Z"
                  fill="currentColor"
                />
              </svg>
            </span>
            <span className="text-base font-semibold tracking-tight text-fg">Nestly</span>
          </Link>

          <h1 className="mt-6 text-display-sm font-semibold text-fg">{title}</h1>
          {subtitle ? (
            <p className="mt-2 text-sm leading-relaxed text-fg-muted text-pretty">{subtitle}</p>
          ) : null}
        </div>

        <div className="rounded-2xl border border-line bg-surface p-6 shadow-md sm:p-7">
          {children}
        </div>

        {footer ? (
          <p className="mt-6 text-center text-sm text-fg-muted">{footer}</p>
        ) : null}
      </div>
    </main>
  );
}

/**
 * Segmented control for mutually-exclusive choices (account type, sign-in
 * method). Replaces rows of primary/secondary Buttons, which read as several
 * competing actions rather than one setting with a current value.
 *
 * Uses real radio semantics: a `tablist` would promise arrow-key tab
 * navigation into associated panels, which is not what these are.
 */
export function Segmented<T extends string>({
  name,
  label,
  options,
  value,
  onChange,
}: {
  name: string;
  label: string;
  options: readonly { value: T; label: string }[];
  value: T;
  onChange: (value: T) => void;
}) {
  return (
    <fieldset>
      <legend className="sr-only">{label}</legend>
      <div className="flex gap-1 rounded-xl bg-surface-2 p-1">
        {options.map((option) => {
          const selected = option.value === value;
          return (
            <label
              key={option.value}
              className={cx(
                "flex flex-1 cursor-pointer items-center justify-center rounded-lg px-3 py-2 text-sm font-medium transition duration-fast ease-out",
                selected
                  ? "bg-surface text-fg shadow-xs"
                  : "text-fg-muted hover:text-fg",
              )}
            >
              <input
                type="radio"
                name={name}
                value={option.value}
                checked={selected}
                onChange={() => onChange(option.value)}
                className="sr-only"
              />
              {option.label}
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}

/**
 * One-time-code input.
 *
 * A single wide field with generous letter-spacing rather than N separate
 * boxes: split-box inputs fight platform SMS autofill, which is the fastest
 * path for the overwhelming majority of real users. `autoComplete="one-time-code"`
 * plus `inputMode="numeric"` is what actually makes iOS and Android offer the
 * code from the message.
 */
export function OtpField({
  label = "Verification code",
  length = 6,
  error,
  hint,
  ...props
}: {
  label?: string;
  length?: number;
  error?: string;
  hint?: string;
} & Omit<React.InputHTMLAttributes<HTMLInputElement>, "type">) {
  const id = props.id ?? `field-${props.name ?? "otp"}`;

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium text-fg">
        {label}
      </label>
      <input
        {...props}
        id={id}
        type="text"
        inputMode="numeric"
        autoComplete="one-time-code"
        maxLength={length}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : hint ? `${id}-hint` : undefined}
        className={cx(
          "w-full rounded-lg border bg-surface px-3 py-3 text-center font-mono text-xl tracking-[0.4em] text-fg shadow-xs outline-none transition duration-fast ease-out",
          "placeholder:tracking-[0.4em] placeholder:text-fg-subtle",
          error
            ? "border-danger focus:border-danger focus:ring-2 focus:ring-danger/25"
            : "border-line hover:border-line-strong focus:border-brand-600 focus:ring-2 focus:ring-brand-600/25",
        )}
        placeholder={"•".repeat(length)}
      />
      {error ? (
        <p id={`${id}-error`} className="text-xs font-medium text-danger">
          {error}
        </p>
      ) : hint ? (
        <p id={`${id}-hint`} className="text-xs text-fg-muted">
          {hint}
        </p>
      ) : null}
    </div>
  );
}

/**
 * Countdown gating a "resend code" action.
 *
 * Without it the only honest thing a resend button can say is nothing, and
 * customers hammer it — every press costs an SMS and, on most gateways, trips
 * rate limiting that locks them out of the account they are trying to reach.
 */
export function useResendCountdown(seconds = 30) {
  const [remaining, setRemaining] = useState(0);
  const intervalRef = useRef<number | null>(null);

  const clear = useCallback(() => {
    if (intervalRef.current !== null) {
      window.clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  const start = useCallback(() => {
    clear();
    setRemaining(seconds);
    intervalRef.current = window.setInterval(() => {
      setRemaining((current) => {
        if (current <= 1) {
          clear();
          return 0;
        }
        return current - 1;
      });
    }, 1000);
  }, [clear, seconds]);

  // Unmounting mid-countdown (navigating away after a successful verify) must
  // not leave the interval running against a dead component.
  useEffect(() => clear, [clear]);

  return { remaining, start, canResend: remaining === 0 };
}

/** "Resend code" row with the countdown applied. */
export function ResendRow({
  remaining,
  canResend,
  onResend,
  pending = false,
}: {
  remaining: number;
  canResend: boolean;
  onResend: () => void;
  pending?: boolean;
}) {
  return (
    <p className="text-center text-sm text-fg-muted">
      Didn&apos;t get the code?{" "}
      {canResend ? (
        <button
          type="button"
          onClick={onResend}
          disabled={pending}
          className="font-medium text-brand-600 underline-offset-4 hover:underline disabled:opacity-55 dark:text-brand-400"
        >
          {pending ? "Sending…" : "Resend"}
        </button>
      ) : (
        <span className="nums text-fg-subtle">Resend in {remaining}s</span>
      )}
    </p>
  );
}
