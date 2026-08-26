"use client";

import { PageHeading } from "@/components/ui";
import { KycSection } from "./_components/KycSection";
import { PhotoSection } from "./_components/PhotoSection";
import { ProfileDetailsSection } from "./_components/ProfileDetailsSection";
import { ReferralPromoSection } from "./_components/ReferralPromoSection";
import { ServiceAreasSection } from "./_components/ServiceAreasSection";
import { SkillsSection } from "./_components/SkillsSection";

/**
 * Provider profile/onboarding (docs/PROVIDER.md's Identity and Capability &
 * Coverage domains), ordered the way onboarding actually runs: who you are,
 * how you look to a customer, prove it, where you work, what you do.
 *
 * Each section owns its own query, mutation and three states, so one failing
 * lookup never blanks out the rest of the screen.
 */
export default function ProfilePage() {
  return (
    <div className="flex w-full max-w-4xl animate-rise flex-col gap-6">
      <PageHeading
        title="Profile"
        subtitle="Your identity, verification status, coverage and skills."
      />
      <ProfileDetailsSection />
      <PhotoSection />
      <KycSection />
      <ServiceAreasSection />
      <SkillsSection />
      <ReferralPromoSection />
    </div>
  );
}
