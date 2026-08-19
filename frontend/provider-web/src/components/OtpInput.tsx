"use client";

import { forwardRef, useId, useState } from "react";
import type { FocusEvent, InputHTMLAttributes } from "react";
import { cx } from "@/components/ui";

type OtpInputProps = {
  label?: string;
  /** Digit count. Matches the backend's OTP contract (`^\d{6}$` -
   *  ProviderLoginValidators.cs / ProviderRegistrationValidators.cs). */
  length?: number;
  error?: string;
  hint?: string;
} & Omit<InputHTMLAttributes<HTMLInputElement>, "type" | "className">;

/**
 * One-time-code entry (task #347).
 *
 * Renders as `length` boxed digits, but underneath is a **single** real
 * `<input>` - a transparent full-width overlay - not `length` independently
 * focusable boxes. That is a deliberate call, not a shortcut:
 *
 * - `auth-ui.tsx`'s previous `OtpField` (a single wide input, no boxes) has
 *   its own comment explaining why: "a single wide field ... rather than N
 *   separate boxes: split-box inputs fight platform SMS autofill ... for a
 *   provider signing in one-handed on site it is the only path worth
 *   optimising for." That reasoning still holds - N real inputs need extra
 *   plumbing (an onPaste/onChange handler on every box that detects a
 *   multi-character value and redistributes it across the rest) to receive
 *   an iOS/Android autofill tap cleanly, and it is easy to get that
 *   plumbing subtly wrong. A single input receives the platform's
 *   `autocomplete="one-time-code"` suggestion the way it was designed to be
 *   received, with zero redistribution logic - the browser fills it, done.
 * - It also makes auto-advance and backspace-to-previous unnecessary rather
 *   than implemented: a real text input's caret already advances on entry
 *   and steps back on backspace with no JS, so there is no custom focus
 *   management to get wrong.
 *
 * The N-boxes requirement is still met visually (task #347's "each box >=
 * 44x44"): the boxes are decorative, driven by the one input's value, and
 * the invisible input spans the full row at `h-11` (44px) tall, so the real
 * tap target is at least as large as - not smaller than - what N separate
 * 44x44 boxes would offer. What is traded away: a screen-reader or
 * keyboard user cannot Tab to a specific digit and correct just that one -
 * there is one focusable control, read as "Verification code" with its
 * current value, the same as the single-input predecessor.
 */
export const OtpInput = forwardRef<HTMLInputElement, OtpInputProps>(function OtpInput(
  { label = "Verification code", length = 6, error, hint, value, onChange, onBlur, id, ...props },
  ref,
) {
  const generatedId = useId();
  const inputId = id ?? `field-${props.name ?? "otp"}-${generatedId}`;
  const [focused, setFocused] = useState(false);

  const digits = String(value ?? "")
    .replace(/\D/g, "")
    .slice(0, length)
    .split("");

  const handleBlur = (event: FocusEvent<HTMLInputElement>) => {
    setFocused(false);
    onBlur?.(event);
  };

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={inputId} className="text-sm font-medium text-fg">
        {label}
      </label>

      <div className="relative">
        <div aria-hidden className="flex gap-2">
          {Array.from({ length }, (_, index) => {
            const filled = index < digits.length;
            // The "next digit goes here" cell - the one place a blinking
            // caret would land in a real per-box layout.
            const isCursor = focused && index === digits.length;
            return (
              <span
                key={index}
                className={cx(
                  "flex h-11 flex-1 items-center justify-center rounded-lg border bg-surface font-mono text-lg font-semibold text-fg shadow-xs transition-colors duration-fast ease-out",
                  error
                    ? "border-danger"
                    : isCursor
                      ? "border-brand-600 ring-2 ring-brand-600/25"
                      : filled
                        ? "border-line-strong"
                        : "border-line",
                )}
              >
                {digits[index] ?? ""}
              </span>
            );
          })}
        </div>

        {/* The one real control. Digits only in `value` - a pasted or
            autofilled string can carry SMS body text around the code, not
            just the code itself - trimmed before it ever reaches the boxes
            or the caller's onChange. `opacity-0` rather than `sr-only`/
            `hidden`: it must stay hit-testable and focusable for touch,
            keyboard and the platform autofill suggestion bar to work. */}
        <input
          {...props}
          ref={ref}
          id={inputId}
          type="text"
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={length}
          value={value}
          onChange={(event) => {
            const digitsOnly = event.target.value.replace(/\D/g, "").slice(0, length);
            if (digitsOnly !== event.target.value) event.target.value = digitsOnly;
            onChange?.(event);
          }}
          onFocus={() => setFocused(true)}
          onBlur={handleBlur}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined}
          className="absolute inset-0 h-11 w-full cursor-text text-transparent caret-transparent opacity-0"
        />
      </div>

      {error ? (
        <p id={`${inputId}-error`} className="text-xs font-medium text-danger">
          {error}
        </p>
      ) : hint ? (
        <p id={`${inputId}-hint`} className="text-xs text-fg-muted">
          {hint}
        </p>
      ) : null}
    </div>
  );
});
