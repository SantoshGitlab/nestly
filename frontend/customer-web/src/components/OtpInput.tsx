"use client";

import { forwardRef, useEffect, useId, useImperativeHandle, useRef, useState } from "react";
import type { ChangeEvent, ClipboardEvent, KeyboardEvent } from "react";
import { cx } from "@/components/ui";

/**
 * Dedicated OTP entry (task #347): `length` separate single-digit boxes, each
 * meeting the 44x44 touch-target minimum directly rather than via hit-slop —
 * a numeric entry box benefits from a large *visible* target, unlike a dense
 * row of icon buttons where slop alone is enough. Auto-advances focus to the
 * next box on digit entry; backspace on an already-empty box moves to and
 * clears the previous one.
 *
 * Supersedes auth-ui.tsx's `OtpField`, which deliberately used one wide field
 * instead of split boxes specifically to avoid fighting platform SMS
 * autofill. That risk is real for a naive per-box implementation, but this
 * one avoids it: a box's `onChange` and `onPaste` both accept a
 * multi-character value — exactly what iOS/Android hand the focused box when
 * the customer taps the QuickType/autofill suggestion, or pastes a forwarded
 * code — and distribute it across the remaining boxes from that position, so
 * autofill fills the whole code in one action either way. The first box
 * carries `autoComplete="one-time-code"`, which is what iOS/Android key off
 * to offer the suggestion at all.
 *
 * Interfaces with react-hook-form's `register(name)` spread exactly like a
 * plain `<input>` would: a single visually-hidden input carries the real
 * `name`/`ref`/value react-hook-form expects, so `formState.errors`,
 * validation-triggered `.focus()`, and submission all keep working unchanged
 * at every call site. `.focus()` on that hidden node is redirected to the
 * first box — react-hook-form calls it after a failed validation, and
 * focusing a node the customer can't see would strand them.
 */
type OtpInputProps = {
  label?: string;
  length?: number;
  error?: string;
  hint?: string;
  name?: string;
  disabled?: boolean;
  onChange?: (event: ChangeEvent<HTMLInputElement>) => void;
  onBlur?: (event: ChangeEvent<HTMLInputElement>) => void;
};

export const OtpInput = forwardRef<HTMLInputElement, OtpInputProps>(function OtpInput(
  { label = "Verification code", length = 6, error, hint, name, disabled, onChange, onBlur },
  outerRef,
) {
  const groupId = `otp-${useId()}`;
  const hiddenRef = useRef<HTMLInputElement | null>(null);
  const boxRefs = useRef<Array<HTMLInputElement | null>>([]);
  const [digits, setDigits] = useState<string[]>(() => Array(length).fill(""));

  useImperativeHandle(outerRef, () => hiddenRef.current as HTMLInputElement, []);

  useEffect(() => {
    const node = hiddenRef.current;
    if (!node) return;
    node.focus = () => boxRefs.current[0]?.focus();
  }, []);

  const emit = (next: string[]) => {
    setDigits(next);
    onChange?.({ target: { name, value: next.join("") } } as ChangeEvent<HTMLInputElement>);
  };

  /** Places digits starting at `index`, advancing/blurring as it fills — the shared path for a single keystroke, a multi-char autofill delivery, and a paste. */
  const applyDigitsFrom = (index: number, raw: string) => {
    const incoming = raw.replace(/\D/g, "").slice(0, length - index);
    if (incoming.length === 0) return;

    const next = [...digits];
    let cursor = index;
    for (const char of incoming) {
      next[cursor] = char;
      cursor += 1;
    }
    emit(next);

    if (cursor >= length) {
      boxRefs.current[length - 1]?.blur();
    } else {
      boxRefs.current[cursor]?.focus();
    }
  };

  const handleChange = (index: number) => (event: ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value;
    if (raw.length === 0) {
      // The box's own digit was deleted directly (not a backspace-on-empty,
      // handled in onKeyDown below) - clear only this position.
      const next = [...digits];
      next[index] = "";
      emit(next);
      return;
    }
    applyDigitsFrom(index, raw);
  };

  const handleKeyDown = (index: number) => (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Backspace" && digits[index] === "" && index > 0) {
      event.preventDefault();
      const next = [...digits];
      next[index - 1] = "";
      emit(next);
      boxRefs.current[index - 1]?.focus();
    } else if (event.key === "ArrowLeft" && index > 0) {
      event.preventDefault();
      boxRefs.current[index - 1]?.focus();
    } else if (event.key === "ArrowRight" && index < length - 1) {
      event.preventDefault();
      boxRefs.current[index + 1]?.focus();
    }
  };

  const handlePaste = (index: number) => (event: ClipboardEvent<HTMLInputElement>) => {
    const text = event.clipboardData.getData("text");
    if (!/\d/.test(text)) return;
    event.preventDefault();
    applyDigitsFrom(index, text);
  };

  const handleBoxBlur = () => {
    onBlur?.({ target: { name, value: digits.join("") } } as ChangeEvent<HTMLInputElement>);
  };

  const describedBy = error ? `${groupId}-error` : hint ? `${groupId}-hint` : undefined;

  return (
    <div className="flex flex-col gap-1.5">
      {/* Real DOM node react-hook-form's `ref`/`register` attach to; the
          visible boxes below are display + input capture only. */}
      <input
        ref={hiddenRef}
        type="text"
        name={name}
        value={digits.join("")}
        readOnly
        tabIndex={-1}
        aria-hidden
        className="sr-only"
      />

      <span id={`${groupId}-label`} className="text-sm font-medium text-fg">
        {label}
      </span>

      <div
        role="group"
        aria-labelledby={`${groupId}-label`}
        aria-describedby={describedBy}
        className="flex flex-wrap gap-2"
      >
        {Array.from({ length }, (_, index) => (
          <input
            key={index}
            ref={(node) => {
              boxRefs.current[index] = node;
            }}
            type="text"
            inputMode="numeric"
            pattern="[0-9]*"
            // Not "1" - a single box left as the paste/autofill target must
            // still accept the full code so applyDigitsFrom can redistribute
            // it, rather than the browser truncating it to one character.
            maxLength={length}
            autoComplete={index === 0 ? "one-time-code" : "off"}
            disabled={disabled}
            value={digits[index] ?? ""}
            aria-label={`Digit ${index + 1} of ${length}`}
            onChange={handleChange(index)}
            onKeyDown={handleKeyDown(index)}
            onPaste={handlePaste(index)}
            onFocus={(event) => event.currentTarget.select()}
            onBlur={handleBoxBlur}
            className={cx(
              "h-14 w-11 shrink-0 rounded-lg border bg-surface text-center text-xl font-semibold text-fg shadow-xs outline-none transition duration-fast ease-out",
              "disabled:cursor-not-allowed disabled:bg-surface-2 disabled:text-fg-subtle",
              error
                ? "border-danger focus:border-danger focus:ring-2 focus:ring-danger/25"
                : "border-line hover:border-line-strong focus:border-brand-600 focus:ring-2 focus:ring-brand-600/25",
            )}
          />
        ))}
      </div>

      {error ? (
        <p id={`${groupId}-error`} className="text-xs font-medium text-danger">
          {error}
        </p>
      ) : hint ? (
        <p id={`${groupId}-hint`} className="text-xs text-fg-muted">
          {hint}
        </p>
      ) : null}
    </div>
  );
});
