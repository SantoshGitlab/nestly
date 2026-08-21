/** Mirrors backend's ProviderReferralSummaryResponse. */
export interface ProviderReferralSummary {
  referralCode: string;
  shareLink: string;
  invitedCount: number;
  qualifiedCount: number;
  rewardedCount: number;
  totalEarned: number;
}

/** Mirrors backend's ProviderReferralHistoryItemResponse. Status is the ProviderReferralStatus enum member name, not a number. */
export interface ProviderReferralHistoryItem {
  id: string;
  refereeDisplayName: string;
  status: "Registered" | "Qualified" | "Rewarded" | "Expired";
  registeredAtUtc: string;
  qualifiedAtUtc: string | null;
  rewardedAtUtc: string | null;
  rewardEarned: number | null;
}
