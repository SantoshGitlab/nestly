"use client";

import { useEffect, useState } from "react";
import { PageHeading } from "@/components/ui";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { CitiesSection } from "./_components/CitiesSection";
import { LocalitiesSection } from "./_components/LocalitiesSection";
import { PincodesSection } from "./_components/PincodesSection";
import { ServiceabilityTabs } from "./_components/ServiceabilityTabs";
import { StatesSection } from "./_components/StatesSection";
import { ZonesSection } from "./_components/ZonesSection";

/**
 * Admin geography master screen (SRS 12.9.1, task 112): state, city, zone,
 * locality and pincode. Gated behind the "serviceability" permission module
 * (task 96a's `AdminPermissionCatalog`) - the route itself is only reachable
 * once `AdminSidebar`/`getVisibleNavModules` already filtered it in, and
 * every mutating control below additionally checks the module's "write"
 * grant (`canWriteModule`) since Read-only Analyst-style roles should still
 * be able to view this screen without seeing edit controls that would only
 * 403 if used.
 */
export default function ServiceabilityPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "serviceability");

  return (
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading
        title="Serviceability"
        subtitle="Geography master: state, city, zone, locality and pincode (SRS 12.9.1)."
      />

      <ServiceabilityTabs />

      <div className="flex flex-col gap-6">
        <StatesSection canWrite={canWrite} />
        <CitiesSection canWrite={canWrite} />
        <ZonesSection canWrite={canWrite} />
        <PincodesSection canWrite={canWrite} />
        <LocalitiesSection canWrite={canWrite} />
      </div>
    </div>
  );
}
