using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 82d: pure reschedule fee-impact math.</summary>
public sealed class RescheduleFeeCalculatorTests
{
    [Fact]
    public void Rescheduling_well_before_the_threshold_owes_no_fee()
    {
        var outcome = RescheduleFeeCalculator.Compute(1000m, TimeSpan.FromHours(48), lateFeeThresholdHours: 6m, lateRescheduleFeePercentage: 10m);

        outcome.IsLate.Should().BeFalse();
        outcome.FeeAmount.Should().Be(0m);
    }

    [Fact]
    public void Rescheduling_inside_the_threshold_charges_the_configured_percentage()
    {
        var outcome = RescheduleFeeCalculator.Compute(1000m, TimeSpan.FromHours(3), lateFeeThresholdHours: 6m, lateRescheduleFeePercentage: 10m);

        outcome.IsLate.Should().BeTrue();
        outcome.FeeAmount.Should().Be(100m);
    }

    [Fact]
    public void An_unpaid_booking_owes_no_fee_even_if_late()
    {
        var outcome = RescheduleFeeCalculator.Compute(0m, TimeSpan.FromMinutes(10), lateFeeThresholdHours: 6m, lateRescheduleFeePercentage: 10m);

        outcome.IsLate.Should().BeTrue();
        outcome.FeeAmount.Should().Be(0m);
    }

    [Fact]
    public void Negative_payable_amount_is_rejected()
    {
        var act = () => RescheduleFeeCalculator.Compute(-1m, TimeSpan.FromHours(10), 6m, 10m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fee_percentage_outside_0_to_100_is_rejected()
    {
        var act = () => RescheduleFeeCalculator.Compute(1000m, TimeSpan.FromHours(1), 6m, 101m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
