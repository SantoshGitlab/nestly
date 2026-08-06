using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 260: pins the midpoint-rounding rule used by the money calculations.
///
/// <c>Math.Round(value, 2)</c> uses banker's rounding
/// (<see cref="MidpointRounding.ToEven"/>) by default, so the percentage
/// discount, tax and subscription-discount calculations were already
/// consistent with the calculators that state ToEven explicitly - but only
/// because of a language default, in code where a silent flip changes what
/// customers are charged. The call sites now say ToEven out loud; these
/// tests fail if any of them is changed to round differently.
///
/// Exact midpoints are the whole point of the fixture: at two decimal places
/// they are the only inputs where ToEven and AwayFromZero disagree.
/// </summary>
public class MoneyRoundingTests
{
    private static Coupon PercentageCoupon(decimal percentage) => new(
        Guid.NewGuid(),
        "SAVE" + Guid.NewGuid().ToString("N")[..6],
        "Rounding fixture",
        CouponDiscountType.Percentage,
        percentage,
        maxDiscountAmount: null,
        minOrderAmount: 0m,
        validFromUtc: DateTime.UtcNow.AddDays(-1),
        validToUtc: DateTime.UtcNow.AddDays(30),
        usageLimitTotal: null,
        usageLimitPerCustomer: null,
        applicableCategoryId: null,
        customerSegment: CouponCustomerSegment.All);

    [Theory]
    // 10% of 100.05 = 10.005 -> ToEven keeps 10.00; AwayFromZero would give 10.01.
    [InlineData(100.05, 10, 10.00)]
    // 10% of 100.15 = 10.015 -> ToEven rounds up to the even 10.02.
    [InlineData(100.15, 10, 10.02)]
    // 5% of 100.10 = 5.005 -> ToEven keeps 5.00.
    [InlineData(100.10, 5, 5.00)]
    // 5% of 100.30 = 5.015 -> ToEven rounds to 5.02.
    [InlineData(100.30, 5, 5.02)]
    public void Percentage_coupon_discount_rounds_half_to_even(decimal orderAmount, decimal percentage, decimal expected)
    {
        PercentageCoupon(percentage).TryCalculateDiscount(orderAmount, out decimal discount)
            .Should().BeTrue();

        discount.Should().Be(expected);
    }

    [Theory]
    [InlineData(100.05, 10, 10.00)]
    [InlineData(100.15, 10, 10.02)]
    public void Commission_rounds_half_to_even_the_same_way(decimal payableAmount, decimal ratePercentage, decimal expected)
    {
        // The reference implementation the others were aligned to - it always
        // stated ToEven explicitly.
        CommissionCalculator.Calculate(payableAmount, ratePercentage).Should().Be(expected);
    }

    [Fact]
    public void A_percentage_discount_never_exceeds_the_order_amount()
    {
        // Guards the clamp TryCalculateDiscount's doc comment promises: no
        // rounding path may make an order free.
        PercentageCoupon(100m).TryCalculateDiscount(0.01m, out decimal discount).Should().BeTrue();

        discount.Should().BeLessThanOrEqualTo(0.01m);
    }
}
