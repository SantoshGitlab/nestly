"use client";

import { useEffect, useState } from "react";
import { PageHeading } from "@/components/ui";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";
import { AddOnPricesSection } from "./_components/AddOnPricesSection";
import { CityPricesSection } from "./_components/CityPricesSection";
import { CityPricingPolicySection } from "./_components/CityPricingPolicySection";
import { PromotionalPricesSection } from "./_components/PromotionalPricesSection";
import { ServicePricesSection } from "./_components/ServicePricesSection";

/**
 * Admin pricing management screen (SRS 12.8, tasks 109a-110): base/add-on/
 * city-wise/promotional price, per-city tax rate, visit charge and platform
 * fee, with effective dating (12.8.2). Gated behind the "pricing" permission
 * module (`AdminPermissionCatalog`) - the route itself is only reachable once
 * `AdminSidebar`/`getVisibleNavModules` already filtered it in, and every
 * mutating control below additionally checks the module's "write" grant
 * (`canWriteModule`) since Read-only Analyst-style roles should still be able
 * to view this screen without seeing edit controls that would only 403 if
 * used. Every write on the backend is recorded in the audit log (SRS 12.8.2
 * "Price change audit required", task 109e) - see `PricingController`/
 * `PricingManagementService`.
 */
export default function PricingPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);

  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);

  const canWrite = canWriteModule(claims, "pricing");

  return (
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading
        title="Pricing"
        subtitle="Base, add-on, city-wise and promotional pricing, plus per-city tax rate, visit charge and platform fee (SRS 12.8)."
      />

      <div className="flex flex-col gap-6">
        <ServicePricesSection canWrite={canWrite} />
        <AddOnPricesSection canWrite={canWrite} />
        <CityPricesSection canWrite={canWrite} />
        <PromotionalPricesSection canWrite={canWrite} />
        <CityPricingPolicySection canWrite={canWrite} />
      </div>
    </div>
  );
}
