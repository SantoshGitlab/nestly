"use client";

import { forwardRef, useId, useState } from "react";
import type { AnimationEvent, ChangeEvent, FocusEvent, InputHTMLAttributes } from "react";
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
 *
 * The boxes are driven by an internal `displayValue` state, not the `value`
 * prop directly: every call site wires this up via
 * `{...form.register("otpCode")}`, and react-hook-form's `register()`
 * deliberately returns only `{ onChange, onBlur, name, ref }` - never
 * `value` - because its whole model for a plain registered input is
 * uncontrolled (it reads the current code straight from the DOM node via
 * `ref` at validation/submit time, same as any native form). That is exactly
 * why submission always worked correctly regardless of what the boxes
 * showed: the real (invisible) input received every keystroke and every
 * autofill natively, and RHF read its true value at submit time. But it
 * also means a `value` prop is never actually supplied here, so computing
 * the boxes from it - as this component did before - left them permanently
 * empty for every provider, typed or autofilled, not just an autofill edge
 * case. `displayValue` fixes that by tracking what the real input holds
 * directly, independent of whether a caller happens to pass a controlled
 * `value` (if one ever does, it still seeds the initial render below).
 */
export const OtpInput = forwardRef<HTMLInputElement, OtpInputProps>(function OtpInput(
  { label = "Verification code", length = 6, error, hint, value, onChange, onBlur, id, ...props },
  ref,
) {
  const generatedId = useId();
  const inputId = id ?? `field-${props.name ?? "otp"}-${generatedId}`;
  const [focused, setFocused] = useState(false);
  const [displayValue, setDisplayValue] = useState(() => String(value ?? ""));

  const digits = displayValue.replace(/\D/g, "").slice(0, length).split("");

  const handleBlur = (event: FocusEvent<HTMLInputElement>) => {
    setFocused(false);
    onBlur?.(event);
  };

  // Some platform autofill (e.g. a phone's "verification code" keyboard
  // suggestion) sets the real input's DOM value without dispatching a
  // React-visible `input`/`change` event, so neither `onChange` below nor
  // `displayValue` learns the fill happened on its own - even though the
  // real code sits in the DOM and gets read correctly at form-submit time.
  // `:-webkit-autofill`/`:autofill` is the one signal browsers reliably
  // apply for a silent fill like that; pairing it with the CSS animation in
  // globals.css turns it into an event this component can react to and
  // force the visible boxes back in sync.
  const handleAutofillDetected = (event: AnimationEvent<HTMLInputElement>) => {
    if (event.animationName !== "nestly-autofill-detect") return;
    const target = event.currentTarget;
    const digitsOnly = target.value.replace(/\D/g, "").slice(0, length);
    if (digitsOnly && digitsOnly !== displayValue) {
      target.value = digitsOnly;
      setDisplayValue(digitsOnly);
      onChange?.({ ...event, target } as unknown as ChangeEvent<HTMLInputElement>);
    }
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
          value={displayValue}
          onChange={(event) => {
            const digitsOnly = event.target.value.replace(/\D/g, "").slice(0, length);
            if (digitsOnly !== event.target.value) event.target.value = digitsOnly;
            setDisplayValue(digitsOnly);
            onChange?.(event);
          }}
          onFocus={() => setFocused(true)}
          onBlur={handleBlur}
          onAnimationStart={handleAutofillDetected}
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
