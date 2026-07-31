"use client";

import { useEffect, useState } from "react";
import { PageHeading } from "@/components/ui";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { CategoryCityMappingSection } from "../_components/CategoryCityMappingSection";
import { ServiceabilityTabs } from "../_components/ServiceabilityTabs";
import { ServicePincodeMappingSection } from "../_components/ServicePincodeMappingSection";

/**
 * Admin serviceability mapping screen (SRS 12.9.2, task 112): which
 * categories are active in which city, and which services are active in
 * which pincode, including reversible suspension/blackout. Same permission
 * gating as the geography master screen - see that page's doc comment.
 */
export default function ServiceabilityMappingsPage() {
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
        title="Serviceability Mapping"
        subtitle="Category/city and service/pincode serviceability, including blackout and suspension (SRS 12.9.2)."
      />

      <ServiceabilityTabs />

      <div className="flex flex-col gap-6">
        <CategoryCityMappingSection canWrite={canWrite} />
        <ServicePincodeMappingSection canWrite={canWrite} />
      </div>
    </div>
  );
}
