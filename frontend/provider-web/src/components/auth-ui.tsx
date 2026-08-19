"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { OfflineBanner } from "@/components/OfflineBanner";
import { cx } from "@/components/ui";

/**
 * Shared furniture for the provider authentication screens (login, register).
 *
 * Ported from customer-web's components/auth-ui.tsx rather than imported: the
 * three frontends are independent Next projects with no shared package, so
 * cross-app imports are not possible and each app carries its own copy. Kept
 * deliberately close to that original so the three auth flows stay
 * recognisably one product; the differences here are the provider wordmark and
 * the full-viewport height (provider-web's auth routes sit outside the
 * `(provider)` group and so render with no app header above them).
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
    <>
      {/* Outside `main`'s centering flex row - a flex sibling here would be
          laid out (and vertically centered) alongside the card instead of
          sitting above it. No other `sticky` element competes for `top: 0`
          on these screens, so it needs no coordinating wrapper (contrast
          `AuthenticatedLayout`, which has one) - except this thin one for
          task #351: `OfflineBanner` is the sole `sticky top-0` element here,
          so unlike `AuthenticatedLayout`'s shared ancestor it could take the
          top-safe-area padding directly, but a wrapper keeps that concern
          out of the shared component (which is also used inside
          `AuthenticatedLayout`'s own ancestor, where the padding must NOT
          also live on `OfflineBanner` itself - see that file's header
          comment - to avoid double-padding when both it and
          `ProviderHeader` are stacked and visible together there). */}
      <div className="pt-[env(safe-area-inset-top)]">
        <OfflineBanner />
      </div>
      <main className="relative isolate flex min-h-screen items-center justify-center overflow-hidden px-4 py-12">
        {/* Decorative brand wash. Sits behind everything and is inert to AT. */}
        <div
          aria-hidden
          className="absolute -top-40 left-1/2 -z-10 h-[28rem] w-[28rem] -translate-x-1/2 rounded-full bg-brand-500/10 blur-3xl"
        />

        <div className="w-full max-w-md animate-rise">
          <div className="mb-8 text-center">
            <Link href="/" className="inline-flex items-center gap-2" aria-label="Nestly Provider home">
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
              <span className="text-base font-semibold tracking-tight text-fg">
                Nestly <span className="text-fg-muted">Provider</span>
              </span>
            </Link>

            <h1 className="mt-6 text-display-sm font-semibold text-fg">{title}</h1>
            {subtitle ? (
              <p className="mt-2 text-sm leading-relaxed text-fg-muted text-pretty">{subtitle}</p>
            ) : null}
          </div>

          <div className="rounded-2xl border border-line bg-surface p-6 shadow-md sm:p-7">
            {children}
          </div>

          {footer ? <p className="mt-6 text-center text-sm text-fg-muted">{footer}</p> : null}
        </div>
      </main>
    </>
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
                selected ? "bg-surface text-fg shadow-xs" : "text-fg-muted hover:text-fg",
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
 * One-time-code input moved to `components/OtpInput.tsx` (task #347) - a
 * dedicated, boxed entry component shared by the login and register OTP
 * steps. See that file's header comment for why it is still backed by a
 * single real `<input>` rather than `length` separately-focusable ones,
 * which is the same reasoning this component used to carry.
 */

/**
 * Countdown gating a "resend code" action.
 *
 * Without it the only honest thing a resend button can say is nothing, and
 * providers hammer it — every press costs an SMS and, on most gateways, trips
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
