using FluentAssertions;
using Nestly.Domain;
using Xunit;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 157's pure commission arithmetic against known rates.</summary>
public sealed class CommissionCalculatorTests
{
    [Theory]
    [InlineData(1000, 15, 150)]        // whole-rupee rate on a whole amount
    [InlineData(999.99, 15, 150.00)]   // 149.9985 rounds up to 150.00 (not a tie - .0085 > half a paisa)
    [InlineData(1250.50, 7.5, 93.79)]  // 93.7875 rounds up to 93.79 (not a tie - .0075 > half a paisa)
    [InlineData(100, 0.325, 0.32)]     // exact tie at 0.325 -> banker's rounding to the nearest even paisa (0.32, not 0.33)
    [InlineData(100, 0, 0)]
    [InlineData(100, 100, 100)]
    public void Calculate_computes_the_commission_amount_for_a_known_rate(decimal payableAmount, decimal ratePercentage, decimal expectedCommission)
    {
        CommissionCalculator.Calculate(payableAmount, ratePercentage).Should().Be(expectedCommission);
    }

    [Fact]
    public void Calculate_rejects_a_negative_payable_amount()
    {
        var act = () => CommissionCalculator.Calculate(-1m, 15m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void Calculate_rejects_a_rate_outside_0_to_100(decimal invalidRate)
    {
        var act = () => CommissionCalculator.Calculate(1000m, invalidRate);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
