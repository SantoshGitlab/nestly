"use client";

import { PageHeading } from "@/components/ui";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
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
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "serviceability");

  return (
    <div className="w-full max-w-6xl">
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
