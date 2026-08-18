using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 356: pure "what is still refundable, out of which funding source" math.</summary>
public sealed class RefundAllocationCalculatorTests
{
    private static RefundTransaction PaymentRefund(decimal amount) =>
        RefundTransaction.ForPayment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RefundType.Partial, RefundMethod.Gateway, amount, "test");

    private static RefundTransaction WalletRefund(decimal amount) =>
        RefundTransaction.ForWalletCredit(Guid.NewGuid(), Guid.NewGuid(), RefundType.Partial, amount, "test");

    [Fact]
    public void A_booking_with_no_refunds_yet_has_both_funding_sources_fully_refundable()
    {
        var remaining = RefundAllocationCalculator.ComputeRemaining(paymentSettledAmount: 700m, walletCreditApplied: 300m, []);

        remaining.PaymentFunded.Should().Be(700m);
        remaining.WalletFunded.Should().Be(300m);
        remaining.Total.Should().Be(1000m);
    }

    [Fact]
    public void Prior_refunds_draw_down_the_funding_source_they_were_raised_against()
    {
        var remaining = RefundAllocationCalculator.ComputeRemaining(
            paymentSettledAmount: 700m, walletCreditApplied: 300m, [PaymentRefund(500m), WalletRefund(100m)]);

        remaining.PaymentFunded.Should().Be(200m);
        remaining.WalletFunded.Should().Be(200m);
    }

    [Fact]
    public void A_failed_refund_moved_no_money_and_so_leaves_the_balance_untouched()
    {
        var failed = PaymentRefund(700m);
        failed.MarkFailed("Gateway declined");

        var remaining = RefundAllocationCalculator.ComputeRemaining(paymentSettledAmount: 700m, walletCreditApplied: 0m, [failed]);

        remaining.PaymentFunded.Should().Be(700m);
    }

    [Fact]
    public void A_fully_wallet_covered_booking_is_refundable_with_no_payment_at_all()
    {
        var remaining = RefundAllocationCalculator.ComputeRemaining(paymentSettledAmount: 0m, walletCreditApplied: 1000m, []);

        remaining.Total.Should().Be(1000m);
        RefundAllocationCalculator.Allocate(1000m, remaining).Should()
            .Be(new RefundAllocationCalculator.Allocation(FromPayment: 0m, FromWallet: 1000m));
    }

    [Fact]
    public void A_refund_that_fits_inside_the_gateway_payment_never_touches_the_wallet()
    {
        var remaining = new RefundAllocationCalculator.Remaining(PaymentFunded: 700m, WalletFunded: 300m);

        RefundAllocationCalculator.Allocate(700m, remaining).Should()
            .Be(new RefundAllocationCalculator.Allocation(FromPayment: 700m, FromWallet: 0m));
    }

    [Fact]
    public void A_refund_larger_than_the_gateway_payment_takes_the_shortfall_out_of_the_wallet()
    {
        var remaining = new RefundAllocationCalculator.Remaining(PaymentFunded: 700m, WalletFunded: 300m);

        RefundAllocationCalculator.Allocate(800m, remaining).Should()
            .Be(new RefundAllocationCalculator.Allocation(FromPayment: 700m, FromWallet: 100m));
    }

    [Fact]
    public void Allocating_more_than_remains_is_a_programming_error_the_caller_must_have_checked_for()
    {
        var remaining = new RefundAllocationCalculator.Remaining(PaymentFunded: 700m, WalletFunded: 300m);

        var act = () => RefundAllocationCalculator.Allocate(1000.01m, remaining);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
