/**
 * Response/request shapes for the Admin API's referral surface (SRS/REFERRAL.md,
 * tasks 167, 170, 171): `ReferralsController` and
 * `ReferralProgramConfigController`. Mirrors
 * `backend/shared/Application/Referral/ReferralAdminContracts.cs` and
 * `ReferralProgramConfigContracts.cs` field for field.
 *
 * AdminApi registers no `JsonStringEnumConverter` (see lib/coupon-types.ts and
 * lib/notification-template-types.ts for the same note), so every enum below
 * arrives over the wire as its **ordinal** and must stay in declaration-order
 * sync with its C# source. The previous version of these screens typed
 * `status` as `string` and rendered it directly, which put a bare `0`/`1`/`2`
 * in the Status column of every row.
 *
 * This lives under the route group rather than in `src/lib` because referral is
 * the only consumer; if a second module ever needs it, move the file.
 */

/** Mirrors `Nestly.Domain.ReferralStatus`'s declaration order exactly. */
export enum ReferralStatus {
  Registered = 0,
  Qualified = 1,
  Rewarded = 2,
  Expired = 3,
}

/** Mirrors `Nestly.Domain.ReferralRewardType`'s declaration order exactly. */
export enum ReferralRewardType {
  WalletCredit = 0,
  Coupon = 1,
}

export const REFERRAL_STATUS_LABELS: Record<ReferralStatus, string> = {
  [ReferralStatus.Registered]: "Registered",
  [ReferralStatus.Qualified]: "Qualified",
  [ReferralStatus.Rewarded]: "Rewarded",
  [ReferralStatus.Expired]: "Expired",
};

export const REFERRAL_REWARD_TYPE_LABELS: Record<ReferralRewardType, string> = {
  [ReferralRewardType.WalletCredit]: "Wallet credit",
  [ReferralRewardType.Coupon]: "Coupon",
};

export interface ReferralAdminListItem {
  id: string;
  referrerCustomerId: string;
  referrerName: string;
  refereeCustomerId: string;
  refereeName: string;
  status: ReferralStatus;
  isFraudFlagged: boolean;
  registeredAtUtc: string;
  rewardedAtUtc: string | null;
}

export interface ReferralAdminSearchResponse {
  items: ReferralAdminListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReferralAdminSearchParams {
  status?: ReferralStatus;
  isFraudFlagged?: boolean;
  customerSearch?: string;
  page?: number;
  pageSize?: number;
}

export interface ReferralAdminDetail {
  id: string;
  referrerCustomerId: string;
  referrerName: string;
  referrerMobile: string;
  refereeCustomerId: string;
  refereeName: string;
  refereeMobile: string;
  referralCodeUsed: string;
  status: ReferralStatus;
  qualifyingBookingId: string | null;
  referrerRewardType: ReferralRewardType;
  referrerRewardValue: number;
  refereeRewardType: ReferralRewardType;
  refereeRewardValue: number;
  minQualifyingOrderAmount: number;
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
export interface ReferralFraudReviewRequest {
  note: string | null;
}

export interface ReferralProgramConfig {
  id: string;
  referrerRewardType: ReferralRewardType;
  referrerRewardValue: number;
  refereeRewardType: ReferralRewardType;
  refereeRewardValue: number;
  minQualifyingOrderAmount: number;
  referralExpiryDays: number;
  maxReferralsPerCustomer: number | null;
  isActive: boolean;
  updatedAtUtc: string;
}

export type ReferralProgramConfigUpdateRequest = Omit<ReferralProgramConfig, "id" | "updatedAtUtc">;

export interface ReferralMilestone {
  id: string;
  thresholdCount: number;
  bonusType: ReferralRewardType;
  bonusValue: number;
  isActive: boolean;
  createdAtUtc: string;
}

export interface ReferralMilestoneCreateRequest {
  thresholdCount: number;
  bonusType: ReferralRewardType;
  bonusValue: number;
}

export interface ReferralFunnelReport {
  invitedCount: number;
  registeredCount: number;
  qualifiedCount: number;
  rewardedCount: number;
  fromUtc: string | null;
  toUtc: string | null;
}

export interface ReferralCostReport {
  totalWalletCreditCost: number;
  totalCouponCost: number;
  totalCost: number;
  rewardedReferralCount: number;
  milestoneBonusCount: number;
  fromUtc: string | null;
  toUtc: string | null;
}
