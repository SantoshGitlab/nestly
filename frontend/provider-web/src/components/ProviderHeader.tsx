"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { NotificationBell } from "@/components/NotificationBell";
import { ThemeToggle } from "@/components/ThemeToggle";
import { cx } from "@/components/ui";
import { API_V1, apiFetch } from "@/lib/api";
import { clearSession, getRefreshToken } from "@/lib/auth";
import { revokeDeviceToken, takeDeviceTokenId } from "@/lib/device-tokens-api";
import type { ProviderSessionClaims } from "@/lib/types";

/** Top chrome bar for the authenticated provider shell. Mirrors admin-web's AdminHeader. */
export function ProviderHeader({ claims }: { claims: ProviderSessionClaims | null }) {
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
    const deviceTokenId = takeDeviceTokenId();

    // Clear locally regardless of the server's answer: the provider asked to
    // be signed out, and a network failure must not leave the token behind.
    try {
      if (refreshToken) {
        await apiFetch(`${API_V1}/auth/logout`, {
          method: "POST",
          body: JSON.stringify({ refreshToken }),
        });
      }
      // Best-effort, same as above: revoking this session's push
      // registration matters (a signed-out device should stop receiving job
      // offers) but must never block sign-out itself.
      if (deviceTokenId) {
        await revokeDeviceToken(deviceTokenId);
      }
    } catch {
      // Already-invalid tokens are a no-op server-side; nothing to report.
    } finally {
      clearSession();
      router.push("/login");
    }
  };

  const mobile = claims?.mobile ?? null;

  return (
    <header className="sticky top-0 z-40 flex h-16 shrink-0 items-center gap-3 border-b border-line bg-surface/80 px-4 backdrop-blur-md sm:px-6">
      {/* Sidebar carries this same lockup from `md` up (ProviderSidebar's
          SidebarBrand) - shown here only where that rail is hidden. */}
      <span className="flex items-center gap-2 md:hidden">
        <span
          aria-hidden
          className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand-gradient text-fg-on-brand shadow-brand"
        >
          <svg viewBox="0 0 24 24" fill="none" className="h-[18px] w-[18px]">
            <path
              d="M4 11.5 12 5l8 6.5V19a1 1 0 0 1-1 1h-4v-5h-6v5H5a1 1 0 0 1-1-1v-7.5Z"
              fill="currentColor"
            />
          </svg>
        </span>
        <span className="text-[0.9375rem] font-semibold tracking-tight text-fg">
          Glavyx <span className="text-fg-muted">Provider</span>
        </span>
      </span>

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
          <span className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-600 text-fg-on-brand">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-4 w-4" aria-hidden>
              <circle cx="12" cy="8" r="3.5" />
              <path d="M5 20a7 7 0 0 1 14 0" strokeLinecap="round" />
            </svg>
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
              <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-brand-600 text-fg-on-brand">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-6 w-6" aria-hidden>
                  <circle cx="12" cy="8" r="3.5" />
                  <path d="M5 20a7 7 0 0 1 14 0" strokeLinecap="round" />
                </svg>
              </span>
              <div className="min-w-0">
                <p className="truncate text-base font-semibold text-fg">{mobile ?? "Signed in"}</p>
                <p className="mt-0.5 truncate text-sm text-fg-muted">Provider</p>
              </div>
            </div>

            <div className="px-6">
              <Link
                href="/profile"
                role="menuitem"
                onClick={() => setMenuOpen(false)}
                className="block rounded-md px-3 py-2 text-sm text-fg transition-colors duration-fast ease-out hover:bg-surface-2 hover:text-brand-600"
              >
                My Profile
              </Link>
              <Link
                href="/profile"
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
