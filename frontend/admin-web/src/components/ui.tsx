"use client";

import { forwardRef } from "react";
import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from "react";

/**
 * The shared primitives every admin screen is built from. Mirrors
 * customer-web/src/components/ui.tsx exactly so the two frontends feel like
 * siblings built the same way (per this branch's brief) - a design system is
 * out of scope here, but duplicating Tailwind class strings across a dozen
 * admin forms is not maintainable either.
 */

export function Card({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: ReactNode;
}) {
  return (
    <section className="w-full rounded-xl border border-black/10 bg-white p-6 shadow-sm dark:border-white/15 dark:bg-neutral-900">
      <h2 className="text-lg font-semibold">{title}</h2>
      {description ? (
        <p className="mt-1 text-sm text-neutral-600 dark:text-neutral-400">
          {description}
        </p>
      ) : null}
      <div className="mt-5">{children}</div>
    </section>
  );
}

export function Alert({
  tone = "error",
  children,
}: {
  tone?: "error" | "success" | "info";
  children: ReactNode;
}) {
  const tones = {
    error:
      "border-red-300 bg-red-50 text-red-900 dark:border-red-900 dark:bg-red-950 dark:text-red-100",
    success:
      "border-green-300 bg-green-50 text-green-900 dark:border-green-900 dark:bg-green-950 dark:text-green-100",
    info: "border-blue-300 bg-blue-50 text-blue-900 dark:border-blue-900 dark:bg-blue-950 dark:text-blue-100",
  } as const;

  return (
    <div
      role={tone === "error" ? "alert" : "status"}
      className={`rounded-lg border px-3 py-2 text-sm ${tones[tone]}`}
    >
      {children}
    </div>
  );
}

interface FieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
}

export const Field = forwardRef<HTMLInputElement, FieldProps>(function Field(
  { label, error, id, ...props },
  ref,
) {
  // Generated from the field name so the label's htmlFor and the input's id
  // always agree - without it, screen readers announce the input unlabelled.
  const inputId = id ?? `field-${props.name ?? label.toLowerCase().replace(/\s+/g, "-")}`;
  const errorId = `${inputId}-error`;

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={inputId} className="text-sm font-medium">
        {label}
      </label>
      <input
        {...props}
        id={inputId}
        ref={ref}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        className="rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
      />
      {error ? (
        <p id={errorId} className="text-xs text-red-600 dark:text-red-400">
          {error}
        </p>
      ) : null}
    </div>
  );
});

interface CheckboxFieldProps {
  label: string;
  description?: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
}

/**
 * Labelled boolean toggle. A controlled checked/onChange pair rather than a
 * forwardRef input like `Field` - boolean form values are more naturally
 * driven through react-hook-form's `Controller` than `register`, and this
 * shape matches that call site directly.
 */
export function CheckboxField({ label, description, checked, onChange, disabled }: CheckboxFieldProps) {
  return (
    <label className="flex items-start gap-3 rounded-lg border border-black/15 px-3 py-2 text-sm dark:border-white/20">
      <input
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        disabled={disabled}
        className="mt-0.5 h-4 w-4 shrink-0 rounded border-black/30 dark:border-white/30"
      />
      <span className="flex flex-col">
        <span className="font-medium">{label}</span>
        {description ? (
          <span className="text-xs text-neutral-600 dark:text-neutral-400">{description}</span>
        ) : null}
      </span>
    </label>
  );
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "danger";
}

export function Button({ variant = "primary", className = "", ...props }: ButtonProps) {
  const variants = {
    primary:
      "bg-black text-white hover:bg-neutral-800 dark:bg-white dark:text-black dark:hover:bg-neutral-200",
    secondary:
      "border border-black/15 hover:bg-black/5 dark:border-white/20 dark:hover:bg-white/10",
    danger:
      "border border-red-300 text-red-700 hover:bg-red-50 dark:border-red-900 dark:text-red-300 dark:hover:bg-red-950",
  } as const;

  return (
    <button
      {...props}
      className={`inline-flex items-center justify-center rounded-lg px-4 py-2 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${variants[variant]} ${className}`}
    />
  );
}

export function PageHeading({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <header className="mb-6">
      <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
      {subtitle ? (
        <p className="mt-1 text-sm text-neutral-600 dark:text-neutral-400">{subtitle}</p>
      ) : null}
    </header>
  );
}
