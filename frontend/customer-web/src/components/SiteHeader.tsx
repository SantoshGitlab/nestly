"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { CitySelector } from "@/components/CitySelector";
import { API_V1, apiFetch } from "@/lib/api";
import {
  clearSession,
  getRefreshToken,
  isAuthenticated,
  subscribeToAuthChanges,
} from "@/lib/auth";

export function SiteHeader() {
  const router = useRouter();
  const [authed, setAuthed] = useState(false);

  useEffect(() => {
    const sync = () => setAuthed(isAuthenticated());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

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
    <header className="border-b border-black/10 dark:border-white/15">
      <nav className="mx-auto flex w-full max-w-6xl flex-wrap items-center justify-between gap-4 px-6 py-4">
        <div className="flex items-center gap-6">
          <Link href="/" className="font-semibold tracking-tight">
            Nestly
          </Link>
          <Link href="/categories" className="text-sm hover:underline">
            Categories
          </Link>
        </div>

        <div className="flex items-center gap-4 text-sm">
          <CitySelector />

          {authed ? (
            <>
              <Link href="/profile" className="hover:underline">
                Profile
              </Link>
              <Link href="/addresses" className="hover:underline">
                Addresses
              </Link>
              <Link href="/bookings" className="hover:underline">
                My bookings
              </Link>
              <Link href="/wallet" className="hover:underline">
                Wallet
              </Link>
              <button type="button" onClick={signOut} className="hover:underline">
                Sign out
              </button>
            </>
          ) : (
            <>
              <Link href="/login" className="hover:underline">
                Sign in
              </Link>
              <Link href="/register" className="hover:underline">
                Create account
              </Link>
            </>
          )}
        </div>
      </nav>
    </header>
  );
}
