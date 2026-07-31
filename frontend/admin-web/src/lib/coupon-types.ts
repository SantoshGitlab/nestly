/**
 * Response/request shapes for the Admin API's coupon surface (SRS 12.12,
 * task 118/119): `CouponsController`. Mirrors the backend contracts in
 * `Application/Coupons/CouponManagementContracts.cs` field for field.
 *
 * AdminApi has no JsonStringEnumConverter registered (see
 * lib/types.ts's CustomerStatus doc comment), so every enum below serialises
 * over the wire as its ordinal and must stay in declaration-order sync with
 * its C# source.
 */

/** Mirrors Nestly.Domain.CouponDiscountType's declaration order exactly. */
export enum CouponDiscountType {
  Percentage = 0,
  Flat = 1,
}

/** Mirrors Nestly.Domain.CouponCustomerSegment's declaration order exactly. */
export enum CouponCustomerSegment {
  All = 0,
  FirstBookingOnly = 1,
  RepeatBookingOnly = 2,
}

/** Lightweight category lookup for the coupon form's "applicable category" picker. */
export interface CategoryLookupResponse {
  id: string;
  name: string;
}

export interface CouponAdminResponse {
  id: string;
  code: string;
  description: string | null;
  discountType: CouponDiscountType;
  discountValue: number;
  maxDiscountAmount: number | null;
  minOrderAmount: number;
  validFromUtc: string;
  validToUtc: string;
  isActive: boolean;
  usageLimitTotal: number | null;
  usageLimitPerCustomer: number | null;
  redemptionCount: number;
  applicableCategoryId: string | null;
  applicableCategoryName: string | null;
  customerSegment: CouponCustomerSegment;
  createdAtUtc: string;
}

export interface CouponAdminSearchResponse {
  items: CouponAdminResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Query parameters for the coupon search endpoint (SRS 12.12.1, task 118). All optional. */
export interface CouponSearchParams {
  code?: string;
  isActive?: boolean;
  discountType?: CouponDiscountType;
  customerSegment?: CouponCustomerSegment;
  applicableCategoryId?: string;
  validOnUtc?: string;
  page?: number;
  pageSize?: number;
}

/** Create request (SRS 12.12.1's field set). Code is set once and never edited afterwards. */
export interface CouponCreateRequest {
  code: string;
  description: string | null;
  discountType: CouponDiscountType;
  discountValue: number;
  maxDiscountAmount: number | null;
  minOrderAmount: number;
  validFromUtc: string;
  validToUtc: string;
  usageLimitTotal: number | null;
  usageLimitPerCustomer: number | null;
  applicableCategoryId: string | null;
  customerSegment: CouponCustomerSegment;
}

/** Edit request for every mutable rule dimension - omits `code` (immutable once created). */
export interface CouponUpdateRequest {
  description: string | null;
  discountType: CouponDiscountType;
  discountValue: number;
  maxDiscountAmount: number | null;
  minOrderAmount: number;
  validFromUtc: string;
  validToUtc: string;
  usageLimitTotal: number | null;
  usageLimitPerCustomer: number | null;
  applicableCategoryId: string | null;
  customerSegment: CouponCustomerSegment;
}

/** One coupon's redemption stats (SRS 12.12.2). */
export interface CouponRedemptionReportRow {
  couponId: string;
  code: string;
  isActive: boolean;
  redemptionCount: number;
  totalDiscountAmount: number;
  uniqueCustomerCount: number;
}

export interface CouponRedemptionReportResponse {
  rows: CouponRedemptionReportRow[];
  totalRedemptions: number;
  totalDiscountAmount: number;
  fromUtc: string | null;
  toUtc: string | null;
}

/** Query parameters for the redemption report endpoint (SRS 12.12.2, task 118). All optional. */
export interface CouponRedemptionReportParams {
  couponId?: string;
  fromUtc?: string;
  toUtc?: string;
}
