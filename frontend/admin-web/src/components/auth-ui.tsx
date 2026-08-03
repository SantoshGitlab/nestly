"use client";

import type { ReactNode } from "react";

/**
 * Shared frame for admin-web's authentication screens.
 *
 * A deliberately trimmed sibling of customer-web/src/components/auth-ui.tsx:
 * the admin app authenticates with email and password only, so the OTP field,
 * resend countdown and account-type segmented control that file carries would
 * be dead code here. The visual frame is kept identical so the three apps'
 * sign-in screens read as one product.
 */
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
    <main className="relative isolate flex min-h-screen items-center justify-center overflow-hidden px-4 py-12">
      {/* Decorative brand wash. Sits behind everything and is inert to AT. */}
      <div
        aria-hidden
        className="absolute -top-40 left-1/2 -z-10 h-[28rem] w-[28rem] -translate-x-1/2 rounded-full bg-brand-500/10 blur-3xl"
      />

      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <span className="inline-flex items-center gap-2">
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
              Nestly <span className="text-fg-muted">Admin</span>
            </span>
          </span>

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
  );
}
