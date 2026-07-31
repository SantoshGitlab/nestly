"use client";

import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { AdminHeader } from "@/components/AdminHeader";
import { AdminSidebar } from "@/components/AdminSidebar";
import { RequireAdminAuth } from "@/components/RequireAdminAuth";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import type { AdminSessionClaims } from "@/lib/types";

/**
 * Authenticated app shell (task 98b): header + sidebar + content area, shown
 * once signed in. Every route nested under the `(admin)` group (this route
 * group segment does not appear in the URL) automatically gets this chrome
 * and the RequireAdminAuth guard - later tasks that add a module's page only
 * need to drop `src/app/(admin)/<module>/page.tsx` in place.
 */
export default function AuthenticatedLayout({ children }: { children: ReactNode }) {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  return (
    <RequireAdminAuth>
      <div className="flex min-h-screen flex-col">
        <AdminHeader claims={claims} />
        <div className="flex flex-1">
          <AdminSidebar claims={claims} />
          <main className="flex-1 p-6">{children}</main>
        </div>
      </div>
    </RequireAdminAuth>
  );
}
