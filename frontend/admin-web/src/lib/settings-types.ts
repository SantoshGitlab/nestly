/**
 * Response/request shapes for the Admin API's system settings surface (SRS
 * 12.19, tasks 131a-131h). Mirrors the backend records in
 * `backend/shared/Application/Settings/SettingsContracts.cs` field-for-field
 * - ASP.NET Core's default controller JSON options camelCase every property,
 * which is what these interfaces assume.
 */

export interface BookingSettings {
  minLeadTimeHours: number;
  maxAdvanceBookingDays: number;
  maxActiveBookingsPerCustomer: number | null;
  allowSameDayBooking: boolean;
}

export interface SlotSettings {
  defaultSlotDurationMinutes: number;
  sameDayCutoffHours: number;
  maxAdvanceBookingDays: number;
  defaultSlotCapacity: number;
  allowOverbooking: boolean;
}

export interface CancellationSettings {
  freeCancellationWindowHours: number;
  lateCancellationFeePercentage: number;
  allowAdminOverride: boolean;
}

export interface RescheduleSettings {
  minHoursBeforeSlot: number;
  maxReschedulesPerBooking: number;
  lateFeeThresholdHours: number;
  lateRescheduleFeePercentage: number;
}

export interface TaxSettings {
  defaultTaxPercentage: number;
  taxRegistrationNumber: string | null;
  taxInclusivePricing: boolean;
}

export interface WalletSettings {
  maxWalletBalance: number;
  maxWalletUsagePercentagePerBooking: number;
  walletCreditExpiryDays: number | null;
  allowWalletTopUp: boolean;
}

export interface CouponSettings {
  maxDiscountPercentagePerCoupon: number;
  maxActiveCouponsPerCustomer: number | null;
  allowCouponStacking: boolean;
  couponsEnabled: boolean;
}

/** Every settings group at once - what `GET /api/v1/settings` returns. */
export interface AllSystemSettingsResponse {
  booking: BookingSettings;
  slot: SlotSettings;
  cancellation: CancellationSettings;
  reschedule: RescheduleSettings;
  tax: TaxSettings;
  wallet: WalletSettings;
  coupon: CouponSettings;
}
