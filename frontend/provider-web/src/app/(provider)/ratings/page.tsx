"use client";

import { PageHeading } from "@/components/ui";
import { useFeatureFlags, useRedirectIfDisabled } from "@/lib/feature-flags";
import { RecentReviewsSection } from "./_components/RecentReviewsSection";
import { SummarySection } from "./_components/SummarySection";

/**
 * Ratings & feedback (docs/OPEN-FIXES-FEATURES.csv "Ratings and feedback"):
 * "ratings are captured from customers and used by the business but never
 * shown back to the professional they describe." This screen closes that
 * loop - the running average up top, recent reviews below.
 *
 * Reached from Profile rather than ProviderSidebar/ProviderTabBar's primary
 * nav (see `RatingsPromoSection`'s doc comment) - a provider checks this
 * occasionally, not on every shift, so it does not compete for a slot in a
 * bottom tab bar already sized for five daily-use screens.
 *
 * Each section owns its own query and its own three states, so a slow or
 * failing reviews list never blanks out the average that loaded fine.
 */
export default function RatingsPage() {
  const { ratingsPageEnabled } = useFeatureFlags();
  useRedirectIfDisabled(ratingsPageEnabled, "/today");

  return (
    <div className="flex w-full max-w-4xl animate-rise flex-col gap-6">
      <PageHeading title="Ratings & feedback" subtitle="How customers have rated your work." />
      <SummarySection />
      <RecentReviewsSection />
    </div>
  );
}
