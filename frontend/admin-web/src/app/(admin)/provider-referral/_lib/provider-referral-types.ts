/**
 * Response/request shapes for the Admin API's provider-referral surface
 * (PROVIDER-REFERRAL.md): `ProviderReferralsController` and
 * `ProviderReferralProgramConfigController`. Mirrors
 * `backend/shared/Application/ProviderReferral/ProviderReferralContracts.cs`
 * field for field - see `(admin)/referral/_lib/referral-types.ts` for the
 * customer-side twin this deliberately parallels.
 *
 * AdminApi registers no `JsonStringEnumConverter`, so `status` arrives over
 * the wire as its ordinal and must stay in declaration-order sync with
 * `Nestly.Domain.ProviderReferralStatus`.
 */

/** Mirrors `Nestly.Domain.ProviderReferralStatus`'s declaration order exactly. */
export enum ProviderReferralStatus {
  Registered = 0,
  Qualified = 1,
  Rewarded = 2,
  Expired = 3,
}

export const PROVIDER_REFERRAL_STATUS_LABELS: Record<ProviderReferralStatus, string> = {
  [ProviderReferralStatus.Registered]: "Registered",
  [ProviderReferralStatus.Qualified]: "Qualified",
  [ProviderReferralStatus.Rewarded]: "Rewarded",
  [ProviderReferralStatus.Expired]: "Expired",
};

export interface ProviderReferralAdminListItem {
  id: string;
  referrerProviderId: string;
  referrerName: string;
  refereeProviderId: string;
  refereeName: string;
  status: ProviderReferralStatus;
  isFraudFlagged: boolean;
  registeredAtUtc: string;
  rewardedAtUtc: string | null;
}

export interface ProviderReferralAdminSearchResponse {
  items: ProviderReferralAdminListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ProviderReferralAdminSearchParams {
  status?: ProviderReferralStatus;
  isFraudFlagged?: boolean;
  providerSearch?: string;
  page?: number;
  pageSize?: number;
}

export interface ProviderReferralAdminDetail {
  id: string;
  referrerProviderId: string;
  referrerName: string;
  referrerPhone: string;
  refereeProviderId: string;
  refereeName: string;
  refereePhone: string;
  referralCodeUsed: string;
  status: ProviderReferralStatus;
  qualifyingBookingId: string | null;
  referrerRewardValue: number;
  refereeRewardValue: number;
  qualifyingCompletedJobsCount: number;
  referrerEarningEntryId: string | null;
  refereeEarningEntryId: string | null;
  registeredAtUtc: string;
  qualifiedAtUtc: string | null;
  rewardedAtUtc: string | null;
  expiresAtUtc: string;
  isFraudFlagged: boolean;
  fraudReviewNote: string | null;
  fraudReviewedByAdminUserId: string | null;
  fraudReviewedAtUtc: string | null;
}

/** Optional admin note attached to a flag/approve/reject decision. */
export interface ProviderReferralFraudReviewRequest {
  note: string | null;
}

export interface ProviderReferralProgramConfig {
  id: string;
  referrerRewardValue: number;
  refereeRewardValue: number;
  qualifyingCompletedJobsCount: number;
  referralExpiryDays: number;
  maxReferralsPerProvider: number | null;
  isActive: boolean;
  updatedAtUtc: string;
}

export type ProviderReferralProgramConfigUpdateRequest = Omit<
  ProviderReferralProgramConfig,
  "id" | "updatedAtUtc"
>;
