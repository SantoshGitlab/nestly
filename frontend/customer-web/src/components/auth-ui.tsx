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

      <div className="w-full max-w-md animate-rise">
        <div className="mb-8 text-center">
          <Link href="/" className="inline-flex items-center gap-2" aria-label="Glavyx home">
            <span
              aria-hidden
              className="flex h-12 w-12 items-center justify-center rounded-xl bg-brand-600 text-fg-on-brand shadow-brand"
            >
              <svg viewBox="0 0 24 24" fill="none" className="h-7 w-7">
                <circle
                  cx="12"
                  cy="12"
                  r="9"
                  stroke="currentColor"
                  strokeWidth="4.5"
                  strokeLinecap="round"
                  strokeDasharray="44 13"
                  transform="rotate(40 12 12)"
                />
                <line x1="13.5" y1="12" x2="21" y2="12" stroke="currentColor" strokeWidth="4.5" strokeLinecap="round" />
              </svg>
            </span>
            <span className="text-lg font-bold tracking-tight text-fg">Glavyx</span>
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
