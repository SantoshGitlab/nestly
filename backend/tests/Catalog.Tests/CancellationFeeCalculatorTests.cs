using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 80b: pure cancellation fee/refund math.</summary>
public sealed class CancellationFeeCalculatorTests
{
    [Fact]
    public void Cancelling_well_before_the_free_window_owes_no_fee_and_refunds_everything()
    {
        var outcome = CancellationFeeCalculator.Compute(
            payableAmount: 1000m, timeUntilSlot: TimeSpan.FromHours(48), freeCancellationWindowHours: 4m, lateCancellationFeePercentage: 20m);

        outcome.WithinFreeWindow.Should().BeTrue();
        outcome.FeeAmount.Should().Be(0m);
        outcome.RefundAmount.Should().Be(1000m);
    }

    [Fact]
    public void Cancelling_exactly_at_the_free_window_boundary_still_owes_no_fee()
    {
        var outcome = CancellationFeeCalculator.Compute(
            payableAmount: 1000m, timeUntilSlot: TimeSpan.FromHours(4), freeCancellationWindowHours: 4m, lateCancellationFeePercentage: 20m);

        outcome.WithinFreeWindow.Should().BeTrue();
        outcome.FeeAmount.Should().Be(0m);
    }

    [Fact]
    public void Cancelling_inside_the_free_window_charges_the_configured_percentage_fee()
    {
        var outcome = CancellationFeeCalculator.Compute(
            payableAmount: 1000m, timeUntilSlot: TimeSpan.FromHours(1), freeCancellationWindowHours: 4m, lateCancellationFeePercentage: 20m);

        outcome.WithinFreeWindow.Should().BeFalse();
        outcome.FeeAmount.Should().Be(200m);
        outcome.RefundAmount.Should().Be(800m);
    }

    [Fact]
    public void Cancelling_after_the_slot_has_already_started_is_treated_as_a_late_cancellation()
    {
        var outcome = CancellationFeeCalculator.Compute(
            payableAmount: 1000m, timeUntilSlot: TimeSpan.FromHours(-2), freeCancellationWindowHours: 4m, lateCancellationFeePercentage: 20m);

        outcome.WithinFreeWindow.Should().BeFalse();
        outcome.FeeAmount.Should().Be(200m);
    }

    [Fact]
    public void An_unpaid_booking_owes_no_fee_and_has_nothing_to_refund_even_if_late()
    {
        var outcome = CancellationFeeCalculator.Compute(
            payableAmount: 0m, timeUntilSlot: TimeSpan.FromMinutes(10), freeCancellationWindowHours: 4m, lateCancellationFeePercentage: 20m);

        outcome.FeeAmount.Should().Be(0m);
        outcome.RefundAmount.Should().Be(0m);
    }

    [Fact]
    public void A_100_percent_fee_refunds_nothing()
    {
        var outcome = CancellationFeeCalculator.Compute(
            payableAmount: 500m, timeUntilSlot: TimeSpan.FromMinutes(30), freeCancellationWindowHours: 4m, lateCancellationFeePercentage: 100m);

        outcome.FeeAmount.Should().Be(500m);
        outcome.RefundAmount.Should().Be(0m);
    }

    [Fact]
    public void Negative_payable_amount_is_rejected()
    {
        var act = () => CancellationFeeCalculator.Compute(-1m, TimeSpan.FromHours(10), 4m, 20m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fee_percentage_outside_0_to_100_is_rejected()
    {
        var act = () => CancellationFeeCalculator.Compute(1000m, TimeSpan.FromHours(1), 4m, 150m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
