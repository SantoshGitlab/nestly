"use client";

import { PageHeading } from "@/components/ui";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { BlackoutsSection } from "./_components/BlackoutsSection";
import { CutoffsSection } from "./_components/CutoffsSection";
import { OverridesSection } from "./_components/OverridesSection";
import { WindowsSection } from "./_components/WindowsSection";

/**
 * Admin slot configuration screen (SRS 12.10, tasks 113a-e/114): recurring
 * windows and their day-of-week rules, holiday/blackout dates, per-city
 * booking cutoffs and advance-booking limits, per-window capacity, and
 * one-off availability overrides. Gated behind the "slots" permission module
 * (task 96a's `AdminPermissionCatalog`) - the route itself is only reachable
 * once `AdminSidebar`/`getVisibleNavModules` already filtered it in, and
 * every mutating control below additionally checks the module's "write"
 * grant (`canWriteModule`), same as the Serviceability screen.
 */
export default function SlotsPage() {
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "slots");

  return (
    <div className="mx-auto w-full max-w-6xl">
      <PageHeading
        title="Slots & Availability"
        subtitle="Slot windows, blackouts, booking cutoffs, capacity and one-off availability overrides (SRS 12.10)."
      />

      <div className="flex flex-col gap-6">
        <WindowsSection canWrite={canWrite} />
        <BlackoutsSection canWrite={canWrite} />
        <CutoffsSection canWrite={canWrite} />
        <OverridesSection canWrite={canWrite} />
      </div>
    </div>
  );
}
