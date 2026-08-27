"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { NotificationBell } from "@/components/NotificationBell";
import { ThemeToggle } from "@/components/ThemeToggle";
import { cx } from "@/components/ui";
import { API_V1, apiFetch } from "@/lib/api";
import { clearSession, getRefreshToken } from "@/lib/auth";
import type { AdminSessionClaims } from "@/lib/types";

/** Top chrome bar for the authenticated admin shell (task 98b). */
export function AdminHeader({
  claims,
  onOpenNav,
}: {
  claims: AdminSessionClaims | null;
  /** Opens the sidebar drawer at widths where the sidebar is hidden. */
  onOpenNav?: () => void;
}) {
  const router = useRouter();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menuOpen) return;

    const onPointerDown = (event: MouseEvent) => {
      if (!menuRef.current?.contains(event.target as Node)) setMenuOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setMenuOpen(false);
    };

    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [menuOpen]);

  const signOut = async () => {
    const refreshToken = getRefreshToken();

    // Clear locally regardless of the server's answer: the admin asked to be
    // signed out, and a network failure must not leave the token behind.
    // Mirrors customer-web/src/components/SiteHeader.tsx's signOut.
    try {
      if (refreshToken) {
        await apiFetch(`${API_V1}/auth/logout`, {
          method: "POST",
          body: JSON.stringify({ refreshToken }),
        });
      }
    } catch {
      // Already-invalid tokens are a no-op server-side; nothing to report.
    } finally {
      clearSession();
      router.push("/login");
    }
  };

  const email = claims?.email ?? null;
  const initial = email?.trim()?.[0]?.toUpperCase() ?? "?";

  return (
    <header className="sticky top-0 z-40 flex h-16 shrink-0 items-center gap-3 border-b border-line bg-surface/80 px-4 backdrop-blur-md sm:px-6">
      {onOpenNav ? (
        <button
          type="button"
          onClick={onOpenNav}
          aria-label="Open navigation"
          className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-fg-muted transition-colors duration-fast ease-out hover:bg-surface-3 hover:text-fg lg:hidden"
        >
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            className="h-5 w-5"
            aria-hidden
          >
            <path d="M4 7h16M4 12h16M4 17h16" />
          </svg>
        </button>
      ) : null}

      <div className="flex-1" />

      <NotificationBell />
      <ThemeToggle />

      <div ref={menuRef} className="relative">
        <button
          type="button"
          onClick={() => setMenuOpen((current) => !current)}
          aria-haspopup="menu"
          aria-expanded={menuOpen}
          aria-label="Account"
          className="flex items-center gap-1"
        >
          <span className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-600 text-sm font-semibold text-fg-on-brand">
            {initial}
          </span>
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            className={cx(
              "h-3 w-3 text-fg-subtle transition-transform duration-fast",
              menuOpen && "rotate-180",
            )}
            aria-hidden
          >
            <path d="m6 9 6 6 6-6" />
          </svg>
        </button>

        {menuOpen ? (
          <div
            role="menu"
            aria-label="Account"
            className="absolute right-0 top-full z-50 mt-2 w-[360px] max-w-[calc(100vw-2rem)] animate-pop overflow-hidden rounded-sm bg-surface pb-4 shadow-sm"
          >
            <div className="mt-5 mb-3 flex items-center gap-4 border-b border-line px-6 pb-5">
              <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-brand-600 text-lg font-semibold text-fg-on-brand">
                {initial}
              </span>
              <div className="min-w-0">
                <p className="truncate text-base font-semibold text-fg">{email ?? "Signed in"}</p>
                {claims?.role ? (
                  <p className="mt-0.5 truncate text-sm text-fg-muted">{claims.role}</p>
                ) : null}
              </div>
            </div>

            <div className="px-6">
              <Link
                href="/settings"
                role="menuitem"
                onClick={() => setMenuOpen(false)}
                className="block rounded-md px-3 py-2 text-sm text-fg transition-colors duration-fast ease-out hover:bg-surface-2 hover:text-brand-600"
              >
                My Profile
              </Link>
              <Link
                href="/settings"
                role="menuitem"
                onClick={() => setMenuOpen(false)}
                className="block rounded-md px-3 py-2 text-sm text-fg transition-colors duration-fast ease-out hover:bg-surface-2 hover:text-brand-600"
              >
                Account Settings
              </Link>
              <button
                type="button"
                role="menuitem"
                onClick={signOut}
                className="block w-full rounded-md px-3 py-2 text-left text-sm text-fg transition-colors duration-fast ease-out hover:bg-surface-2 hover:text-brand-600"
              >
                Sign Out
              </button>
            </div>
          </div>
        ) : null}
      </div>
    </header>
  );
}
