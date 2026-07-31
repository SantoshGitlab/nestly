"use client";

import { useEffect, useState } from "react";
import { Card, PageHeading } from "@/components/ui";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { getVisibleNavModules } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";

/**
 * Dashboard landing page (SRS 12.3). This is intentionally a minimal shell,
 * not the full KPI dashboard - the widgets in SRS 12.3.1/12.3.2 are separate,
 * not-yet-implemented work. This page exists because task 98b/98d need a
 * real post-login landing route to redirect to and to prove the
 * authenticated layout renders content correctly.
 */
export default function DashboardPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const modules = getVisibleNavModules(claims);

  return (
    <div className="mx-auto w-full max-w-4xl">
      <PageHeading
        title="Dashboard"
        subtitle={claims?.role ? `Signed in as ${claims.role}.` : "Signed in."}
      />

      <Card
        title="Available modules"
        description="Based on your current role/permissions. KPI widgets (SRS 12.3.1) land in a later task."
      >
        <ul className="grid grid-cols-2 gap-2 text-sm sm:grid-cols-3">
          {modules.map((module) => (
            <li key={module.key} className="rounded-lg border border-black/10 px-3 py-2 dark:border-white/15">
              {module.label}
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}
