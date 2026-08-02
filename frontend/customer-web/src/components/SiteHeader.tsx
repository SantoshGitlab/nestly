"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { CitySelector } from "@/components/CitySelector";
import { ThemeToggle } from "@/components/ThemeToggle";
import { Button, cx } from "@/components/ui";
import { API_V1, apiFetch } from "@/lib/api";
import {
  clearSession,
  getRefreshToken,
  isAuthenticated,
  subscribeToAuthChanges,
} from "@/lib/auth";

/**
 * Global chrome for customer-web.
 *
 * The previous header put all nine signed-in destinations in one flat row,
 * which wrapped into an unreadable block on a laptop and was unusable on a
 * phone. Only the genuinely navigational links stay in the bar; the rest are
 * account destinations and now sit behind an account menu, with the same set
 * mirrored into a drawer at mobile widths.
 */

/** Account-menu destinations, in the order they matter to a signed-in customer. */
const ACCOUNT_LINKS = [
  { href: "/bookings", label: "My bookings" },
  { href: "/recurring-bookings", label: "Recurring bookings" },
  { href: "/addresses", label: "Addresses" },
  { href: "/wallet", label: "Wallet" },
  { href: "/subscription", label: "Nestly Plus" },
  { href: "/refer-earn", label: "Refer & Earn" },
  { href: "/support", label: "Support" },
  { href: "/profile", label: "Profile" },
] as const;

export function SiteHeader() {
  const router = useRouter();
  const pathname = usePathname();
  const [authed, setAuthed] = useState(false);
  const [drawerOpen, setDrawerOpen] = useState(false);

  useEffect(() => {
    const sync = () => setAuthed(isAuthenticated());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  // A route change means the destination was reached — leaving the drawer open
  // over the new page is never what the customer wanted.
  useEffect(() => {
    setDrawerOpen(false);
  }, [pathname]);

  const signOut = async () => {
    const refreshToken = getRefreshToken();

    // Clear locally regardless of the server's answer: the customer asked to
    // be signed out, and a network failure must not leave the token behind.
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

  return (
    <header className="sticky top-0 z-40 border-b border-line bg-bg/80 backdrop-blur-md">
      <nav
        aria-label="Primary"
        className="mx-auto flex h-16 w-full max-w-6xl items-center gap-3 px-4 sm:px-6"
      >
        <Link href="/" className="flex shrink-0 items-center gap-2" aria-label="Nestly home">
          <NestlyMark />
          <span className="text-[0.9375rem] font-semibold tracking-tight text-fg">Nestly</span>
        </Link>

        <div className="ml-2 hidden items-center gap-1 md:flex">
          <NavLink href="/categories" active={pathname.startsWith("/categories")}>
            Categories
          </NavLink>
          <NavLink href="/search" active={pathname.startsWith("/search")}>
            Search
          </NavLink>
        </div>

        <div className="flex-1" />

        <div className="hidden items-center gap-2 md:flex">
          <CitySelector />
          <ThemeToggle />
          {authed ? (
            <AccountMenu onSignOut={signOut} pathname={pathname} />
          ) : (
            <>
              <Button variant="ghost" size="sm" onClick={() => router.push("/login")}>
                Sign in
              </Button>
              <Button size="sm" onClick={() => router.push("/register")}>
                Create account
              </Button>
            </>
          )}
        </div>

        <div className="flex items-center gap-1 md:hidden">
          <ThemeToggle />
          <button
            type="button"
            onClick={() => setDrawerOpen(true)}
            aria-label="Open menu"
            aria-expanded={drawerOpen}
            className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-fg-muted transition-colors duration-fast ease-out hover:bg-surface-3 hover:text-fg"
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
        </div>
      </nav>

      {drawerOpen ? (
        <MobileDrawer
          authed={authed}
          pathname={pathname}
          onClose={() => setDrawerOpen(false)}
          onSignOut={signOut}
        />
      ) : null}
    </header>
  );
}

function NestlyMark() {
  return (
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
  );
}

function NavLink({
  href,
  active,
  children,
}: {
  href: string;
  active: boolean;
  children: ReactNode;
}) {
  return (
    <Link
      href={href}
      aria-current={active ? "page" : undefined}
      className={cx(
        "rounded-lg px-3 py-2 text-sm font-medium transition-colors duration-fast ease-out",
        active ? "bg-surface-3 text-fg" : "text-fg-muted hover:bg-surface-3 hover:text-fg",
      )}
    >
      {children}
    </Link>
  );
}

/** Signed-in account dropdown. Closes on Escape, outside click, and route change. */
function AccountMenu({ onSignOut, pathname }: { onSignOut: () => void; pathname: string }) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setOpen(false);
  }, [pathname]);

  useEffect(() => {
    if (!open) return;

    const onPointerDown = (event: MouseEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };

    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((current) => !current)}
        aria-haspopup="menu"
        aria-expanded={open}
        className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-2.5 text-sm font-medium text-fg shadow-xs transition-colors duration-fast ease-out hover:border-line-strong hover:bg-surface-2"
      >
        <span className="flex h-6 w-6 items-center justify-center rounded-full bg-brand-600 text-fg-on-brand">
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            className="h-3.5 w-3.5"
            aria-hidden
          >
            <circle cx="12" cy="8" r="3.5" />
            <path d="M5 20a7 7 0 0 1 14 0" strokeLinecap="round" />
          </svg>
        </span>
        <span className="hidden lg:inline">Account</span>
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          className={cx(
            "h-3.5 w-3.5 text-fg-subtle transition-transform duration-fast",
            open && "rotate-180",
          )}
          aria-hidden
        >
          <path d="m6 9 6 6 6-6" />
        </svg>
      </button>

      {open ? (
        <div
          role="menu"
          className="absolute right-0 top-full z-50 mt-2 w-56 animate-pop overflow-hidden rounded-xl border border-line bg-surface p-1.5 shadow-lg"
        >
          {ACCOUNT_LINKS.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              role="menuitem"
              className={cx(
                "block rounded-lg px-3 py-2 text-sm transition-colors duration-fast ease-out",
                pathname.startsWith(link.href)
                  ? "bg-surface-3 font-medium text-fg"
                  : "text-fg-muted hover:bg-surface-2 hover:text-fg",
              )}
            >
              {link.label}
            </Link>
          ))}
          <div className="my-1.5 border-t border-line" />
          <button
            type="button"
            role="menuitem"
            onClick={onSignOut}
            className="block w-full rounded-lg px-3 py-2 text-left text-sm text-danger transition-colors duration-fast ease-out hover:bg-danger-soft"
          >
            Sign out
          </button>
        </div>
      ) : null}
    </div>
  );
}

/** Full-height slide-over for mobile widths, holding the same destinations. */
function MobileDrawer({
  authed,
  pathname,
  onClose,
  onSignOut,
}: {
  authed: boolean;
  pathname: string;
  onClose: () => void;
  onSignOut: () => void;
}) {
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    panelRef.current?.focus();

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKeyDown);

    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = previousOverflow;
    };
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-50 md:hidden">
      <div
        className="absolute inset-0 animate-fade-in bg-overlay/50 backdrop-blur-[2px]"
        onClick={onClose}
        aria-hidden
      />
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label="Menu"
        tabIndex={-1}
        className="absolute inset-y-0 right-0 flex w-[min(20rem,85vw)] animate-rise flex-col border-l border-line bg-surface outline-none"
      >
        <div className="flex h-16 shrink-0 items-center justify-between border-b border-line px-4">
          <span className="text-sm font-semibold text-fg">Menu</span>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close menu"
            className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-fg-muted hover:bg-surface-3 hover:text-fg"
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
              <path d="M18 6 6 18M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-4">
          <div className="mb-4">
            <CitySelector />
          </div>

          <DrawerLink href="/categories" pathname={pathname}>
            Categories
          </DrawerLink>
          <DrawerLink href="/search" pathname={pathname}>
            Search
          </DrawerLink>

          {authed ? (
            <>
              <p className="mb-1 mt-5 px-3 text-xs font-semibold uppercase tracking-wide text-fg-subtle">
                Account
              </p>
              {ACCOUNT_LINKS.map((link) => (
                <DrawerLink key={link.href} href={link.href} pathname={pathname}>
                  {link.label}
                </DrawerLink>
              ))}
            </>
          ) : null}
        </div>

        <div className="shrink-0 border-t border-line p-4">
          {authed ? (
            <Button variant="secondary" fullWidth onClick={onSignOut}>
              Sign out
            </Button>
          ) : (
            // Real links rather than Buttons with router.push: these are plain
            // navigations, so they must stay middle-clickable and openable in
            // a new tab.
            <div className="flex flex-col gap-2">
              <Link
                href="/register"
                className="inline-flex h-10 w-full items-center justify-center rounded-lg bg-brand-600 text-sm font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700 active:scale-[0.98]"
              >
                Create account
              </Link>
              <Link
                href="/login"
                className="inline-flex h-10 w-full items-center justify-center rounded-lg border border-line bg-surface text-sm font-medium text-fg shadow-xs transition duration-fast ease-out hover:border-line-strong hover:bg-surface-2 active:scale-[0.98]"
              >
                Sign in
              </Link>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function DrawerLink({
  href,
  pathname,
  children,
}: {
  href: string;
  pathname: string;
  children: ReactNode;
}) {
  const active = pathname === href || pathname.startsWith(`${href}/`);
  return (
    <Link
      href={href}
      aria-current={active ? "page" : undefined}
      className={cx(
        "block rounded-lg px-3 py-2.5 text-sm transition-colors duration-fast ease-out",
        active
          ? "bg-surface-3 font-medium text-fg"
          : "text-fg-muted hover:bg-surface-2 hover:text-fg",
      )}
    >
      {children}
    </Link>
  );
}
