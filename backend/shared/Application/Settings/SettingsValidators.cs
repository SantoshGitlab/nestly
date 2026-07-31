using FluentValidation;

namespace Nestly.Application.Settings;

public class BookingSettingsValidator : AbstractValidator<BookingSettings>
{
    public BookingSettingsValidator()
    {
        RuleFor(x => x.MinLeadTimeHours).InclusiveBetween(0, 720);
        RuleFor(x => x.MaxAdvanceBookingDays).InclusiveBetween(1, 365);
        RuleFor(x => x.MaxActiveBookingsPerCustomer).InclusiveBetween(1, 1000).When(x => x.MaxActiveBookingsPerCustomer is not null);
    }
}

public class SlotSettingsValidator : AbstractValidator<SlotSettings>
{
    public SlotSettingsValidator()
    {
        RuleFor(x => x.DefaultSlotDurationMinutes).InclusiveBetween(15, 480);
        RuleFor(x => x.SameDayCutoffHours).InclusiveBetween(0, 24);
        RuleFor(x => x.MaxAdvanceBookingDays).InclusiveBetween(1, 365);
        RuleFor(x => x.DefaultSlotCapacity).InclusiveBetween(1, 1000);
    }
}

public class CancellationSettingsValidator : AbstractValidator<CancellationSettings>
{
    public CancellationSettingsValidator()
    {
        RuleFor(x => x.FreeCancellationWindowHours).InclusiveBetween(0, 720);
        RuleFor(x => x.LateCancellationFeePercentage).InclusiveBetween(0, 100);
    }
}

public class RescheduleSettingsValidator : AbstractValidator<RescheduleSettings>
{
    public RescheduleSettingsValidator()
    {
        RuleFor(x => x.MinHoursBeforeSlot).InclusiveBetween(0, 720);
        RuleFor(x => x.MaxReschedulesPerBooking).InclusiveBetween(0, 50);
        RuleFor(x => x.LateFeeThresholdHours).InclusiveBetween(0, 720);
        RuleFor(x => x.LateRescheduleFeePercentage).InclusiveBetween(0, 100);
    }
}

public class TaxSettingsValidator : AbstractValidator<TaxSettings>
{
    public TaxSettingsValidator()
    {
        RuleFor(x => x.DefaultTaxPercentage).InclusiveBetween(0, 100);
        RuleFor(x => x.TaxRegistrationNumber).MaximumLength(50);
    }
}

public class WalletSettingsValidator : AbstractValidator<WalletSettings>
{
    public WalletSettingsValidator()
    {
        RuleFor(x => x.MaxWalletBalance).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxWalletUsagePercentagePerBooking).InclusiveBetween(0, 100);
        RuleFor(x => x.WalletCreditExpiryDays).InclusiveBetween(1, 3650).When(x => x.WalletCreditExpiryDays is not null);
    }
}

public class CouponSettingsValidator : AbstractValidator<CouponSettings>
{
    public CouponSettingsValidator()
    {
        RuleFor(x => x.MaxDiscountPercentagePerCoupon).InclusiveBetween(0, 100);
        RuleFor(x => x.MaxActiveCouponsPerCustomer).InclusiveBetween(1, 100).When(x => x.MaxActiveCouponsPerCustomer is not null);
    }
}
