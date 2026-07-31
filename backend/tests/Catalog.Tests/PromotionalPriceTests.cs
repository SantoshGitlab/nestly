using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Domain rules for <see cref="PromotionalPrice"/> (SRS 12.8.1 "Promotional price", task 109a).</summary>
public sealed class PromotionalPriceTests
{
    [Fact]
    public void Constructor_rejects_a_non_positive_discounted_price()
    {
        Action act = () => new PromotionalPrice(
            Guid.NewGuid(), Guid.NewGuid(), null, 0, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_rejects_a_start_date_after_the_end_date()
    {
        Action act = () => new PromotionalPrice(
            Guid.NewGuid(), Guid.NewGuid(), null, 100, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_new_promotion_is_active_by_default()
    {
        var promotion = new PromotionalPrice(
            Guid.NewGuid(), Guid.NewGuid(), null, 100, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        promotion.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsEffectiveOn_is_true_only_within_the_date_range_while_active()
    {
        var promotion = new PromotionalPrice(
            Guid.NewGuid(), Guid.NewGuid(), null, 100, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20));

        promotion.IsEffectiveOn(new DateOnly(2026, 1, 9)).Should().BeFalse();
        promotion.IsEffectiveOn(new DateOnly(2026, 1, 10)).Should().BeTrue();
        promotion.IsEffectiveOn(new DateOnly(2026, 1, 20)).Should().BeTrue();
        promotion.IsEffectiveOn(new DateOnly(2026, 1, 21)).Should().BeFalse();
    }

    [Fact]
    public void IsEffectiveOn_is_false_once_deactivated_even_within_the_date_range()
    {
        var promotion = new PromotionalPrice(
            Guid.NewGuid(), Guid.NewGuid(), null, 100, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        promotion.Deactivate();

        promotion.IsEffectiveOn(new DateOnly(2026, 1, 15)).Should().BeFalse();
    }

    [Fact]
    public void SetDateRange_rejects_a_start_date_after_the_end_date()
    {
        var promotion = new PromotionalPrice(
            Guid.NewGuid(), Guid.NewGuid(), null, 100, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Action act = () => promotion.SetDateRange(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetDiscountedPrice_rejects_a_non_positive_value()
    {
        var promotion = new PromotionalPrice(
            Guid.NewGuid(), Guid.NewGuid(), null, 100, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Action act = () => promotion.SetDiscountedPrice(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
