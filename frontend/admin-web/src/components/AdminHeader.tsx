"use client";

import { useRouter } from "next/navigation";
import { API_V1, apiFetch } from "@/lib/api";
import { clearSession, getRefreshToken } from "@/lib/auth";
import type { AdminSessionClaims } from "@/lib/types";

/** Top chrome bar for the authenticated admin shell (task 98b). */
export function AdminHeader({ claims }: { claims: AdminSessionClaims | null }) {
  const router = useRouter();

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

  return (
    <header className="flex items-center justify-between border-b border-black/10 px-6 py-4 dark:border-white/15">
      <span className="font-semibold tracking-tight">Nestly Admin</span>

      <div className="flex items-center gap-4 text-sm">
        {claims?.email ? (
          <span className="text-neutral-600 dark:text-neutral-400">
            {claims.email}
            {claims.role ? ` · ${claims.role}` : ""}
          </span>
        ) : null}
        <button type="button" onClick={signOut} className="hover:underline">
          Sign out
        </button>
      </div>
    </header>
  );
}
