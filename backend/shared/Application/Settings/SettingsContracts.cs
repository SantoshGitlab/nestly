namespace Nestly.Application.Settings;

/// <summary>
/// Booking rules settings group (SRS 12.19 "Booking rules", task 131a).
/// Governs how far ahead and how close to a slot a booking may be created -
/// distinct from <see cref="SlotSettings"/>, which governs how slots
/// themselves are generated.
/// </summary>
/// <param name="MinLeadTimeHours">A booking must start at least this many hours from now.</param>
/// <param name="MaxAdvanceBookingDays">A booking cannot be made more than this many days ahead.</param>
/// <param name="MaxActiveBookingsPerCustomer">Cap on a customer's simultaneous non-terminal bookings; null = unlimited.</param>
/// <param name="AllowSameDayBooking">Whether a booking may be created for the current calendar day at all.</param>
public sealed record BookingSettings(
    int MinLeadTimeHours,
    int MaxAdvanceBookingDays,
    int? MaxActiveBookingsPerCustomer,
    bool AllowSameDayBooking);

/// <summary>
/// Slot engine settings group (SRS 12.19 "Slot rules", SRS 15.2, task 131b).
/// </summary>
/// <param name="DefaultSlotDurationMinutes">Length of a generated slot window.</param>
/// <param name="SameDayCutoffHours">Same-day slots starting within this many hours are no longer offered (SRS 15.2 "same-day cutoff rules").</param>
/// <param name="MaxAdvanceBookingDays">How many days ahead slots are generated/offered (SRS 15.2 "advance booking days must be configurable").</param>
/// <param name="DefaultSlotCapacity">Default concurrent-booking capacity for a newly generated slot.</param>
/// <param name="AllowOverbooking">Whether a slot may accept bookings beyond its configured capacity.</param>
public sealed record SlotSettings(
    int DefaultSlotDurationMinutes,
    int SameDayCutoffHours,
    int MaxAdvanceBookingDays,
    int DefaultSlotCapacity,
    bool AllowOverbooking);

/// <summary>
/// Cancellation policy settings group (SRS 12.19 "Cancellation rules", SRS
/// 11.14.1, task 131c). Field shape mirrors the existing
/// <c>CancellationPolicyOptions</c> appsettings binding this group is meant
/// to become the admin-editable front end for.
/// </summary>
/// <param name="FreeCancellationWindowHours">Cancelling at least this many hours before the slot owes no fee.</param>
/// <param name="LateCancellationFeePercentage">Percentage of the payable amount retained when cancelling inside the free window.</param>
/// <param name="AllowAdminOverride">Whether an admin may waive the late-cancellation fee on a case-by-case basis.</param>
public sealed record CancellationSettings(
    decimal FreeCancellationWindowHours,
    decimal LateCancellationFeePercentage,
    bool AllowAdminOverride);

/// <summary>
/// Reschedule policy settings group (SRS 12.19 "Reschedule rules", SRS
/// 11.15.1, task 131d). Field shape mirrors the existing
/// <c>ReschedulePolicyOptions</c> appsettings binding this group is meant to
/// become the admin-editable front end for.
/// </summary>
/// <param name="MinHoursBeforeSlot">Rescheduling with less than this many hours to the current slot is blocked entirely.</param>
/// <param name="MaxReschedulesPerBooking">How many times a single booking may be rescheduled.</param>
/// <param name="LateFeeThresholdHours">Rescheduling with less than this many hours to go (but above <see cref="MinHoursBeforeSlot"/>) incurs a fee.</param>
/// <param name="LateRescheduleFeePercentage">Percentage of the booking's payable amount charged as a late-reschedule fee.</param>
public sealed record RescheduleSettings(
    decimal MinHoursBeforeSlot,
    int MaxReschedulesPerBooking,
    decimal LateFeeThresholdHours,
    decimal LateRescheduleFeePercentage);

/// <summary>Tax settings group (SRS 12.19 "Tax settings", task 131e).</summary>
/// <param name="DefaultTaxPercentage">Default GST/tax rate applied to a booking (0-100), used where no city-specific override (<c>CityPricingPolicy</c>) exists.</param>
/// <param name="TaxRegistrationNumber">Platform's tax/GST registration number, shown on customer invoices; null if not yet configured.</param>
/// <param name="TaxInclusivePricing">Whether displayed service prices already include tax.</param>
public sealed record TaxSettings(
    decimal DefaultTaxPercentage,
    string? TaxRegistrationNumber,
    bool TaxInclusivePricing);

/// <summary>Wallet settings group (SRS 12.19 "Wallet settings", SRS 14.5, task 131f).</summary>
/// <param name="MaxWalletBalance">Upper bound a customer's wallet balance may reach.</param>
/// <param name="MaxWalletUsagePercentagePerBooking">Cap on how much of a single booking's payable amount may be covered from wallet balance (0-100).</param>
/// <param name="WalletCreditExpiryDays">Days after which a wallet credit expires; null = credits never expire.</param>
/// <param name="AllowWalletTopUp">Whether customers may add funds to their wallet directly (as opposed to only receiving refund/cashback credits).</param>
public sealed record WalletSettings(
    decimal MaxWalletBalance,
    decimal MaxWalletUsagePercentagePerBooking,
    int? WalletCreditExpiryDays,
    bool AllowWalletTopUp);

/// <summary>
/// Coupon settings group (SRS 12.19 "Coupon settings", SRS 14.2, task 131g).
/// Platform-wide guardrails that apply across every <c>Coupon</c>, distinct
/// from a single coupon's own fields (code, discount value, validity window)
/// on the <c>Coupon</c> aggregate itself.
/// </summary>
/// <param name="MaxDiscountPercentagePerCoupon">Upper bound any individual coupon's percentage discount may be configured to (0-100).</param>
/// <param name="MaxActiveCouponsPerCustomer">Cap on how many distinct coupons a customer may redeem while active; null = unlimited.</param>
/// <param name="AllowCouponStacking">Whether more than one coupon may be applied to the same booking.</param>
/// <param name="CouponsEnabled">Platform-wide feature flag - when false, coupon redemption is disabled everywhere regardless of individual coupon state (SRS 12.19 "Feature flags").</param>
public sealed record CouponSettings(
    decimal MaxDiscountPercentagePerCoupon,
    int? MaxActiveCouponsPerCustomer,
    bool AllowCouponStacking,
    bool CouponsEnabled);

/// <summary>Every settings group at once, for the admin Settings landing page (task 131h).</summary>
public sealed record AllSystemSettingsResponse(
    BookingSettings Booking,
    SlotSettings Slot,
    CancellationSettings Cancellation,
    RescheduleSettings Reschedule,
    TaxSettings Tax,
    WalletSettings Wallet,
    CouponSettings Coupon);
