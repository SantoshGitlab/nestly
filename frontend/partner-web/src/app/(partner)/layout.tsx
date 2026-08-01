"use client";

import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { PartnerHeader } from "@/components/PartnerHeader";
import { PartnerSidebar } from "@/components/PartnerSidebar";
import { RequirePartnerAuth } from "@/components/RequirePartnerAuth";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import type { PartnerSessionClaims } from "@/lib/types";

/**
 * Authenticated app shell: header + sidebar + content area, shown once
 * signed in. Every route nested under the `(partner)` route group (this
 * segment does not appear in the URL) automatically gets this chrome and
 * the RequirePartnerAuth guard. Mirrors admin-web's `(admin)/layout.tsx`.
 */
export default function AuthenticatedLayout({ children }: { children: ReactNode }) {
  const [claims, setClaims] = useState<PartnerSessionClaims | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  return (
    <RequirePartnerAuth>
      <div className="flex min-h-screen flex-col">
        <PartnerHeader claims={claims} />
        <div className="flex flex-1">
          <PartnerSidebar />
          <main className="flex-1 p-6">{children}</main>
        </div>
      </div>
    </RequirePartnerAuth>
  );
}
