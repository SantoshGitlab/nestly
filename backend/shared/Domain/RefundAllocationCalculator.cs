namespace Nestly.Domain;

/// <summary>
/// Pure "how much of a booking is still refundable, and out of which funding
/// source" math. Same split as <see cref="CancellationFeeCalculator"/>: the
/// arithmetic lives here with no repository or DI plumbing, so both
/// <c>RefundService</c> (which moves the money) and <c>CancellationService</c>
/// (which only needs the refundable base to compute a fee against) read the
/// same rule instead of each keeping their own copy of it.
///
/// A booking can be funded from two independent sources: its gateway
/// <see cref="PaymentTransaction"/>, and the wallet balance it consumed at
/// checkout (<see cref="Booking.WalletCreditAppliedSnapshot"/>). Either can be
/// zero - a fully wallet-covered booking has no payment at all (task 331), and
/// most bookings spend no wallet balance.
/// </summary>
public static class RefundAllocationCalculator
{
    /// <summary>What is still refundable, per funding source, after every non-failed refund already raised against the booking.</summary>
    public sealed record Remaining(decimal PaymentFunded, decimal WalletFunded)
    {
        public decimal Total => PaymentFunded + WalletFunded;
    }

    /// <summary>How one refund request splits across the two funding sources.</summary>
    public sealed record Allocation(decimal FromPayment, decimal FromWallet);

    /// <param name="paymentSettledAmount">The booking's successfully paid gateway amount, or 0 when it has no successful payment.</param>
    /// <param name="walletCreditApplied">The wallet balance the booking consumed at checkout, or 0 when it consumed none.</param>
    /// <param name="priorRefunds">Every refund ever raised against the booking; failed ones are ignored, since they moved no money.</param>
    public static Remaining ComputeRemaining(
        decimal paymentSettledAmount, decimal walletCreditApplied, IEnumerable<RefundTransaction> priorRefunds)
    {
        ArgumentNullException.ThrowIfNull(priorRefunds);

        if (paymentSettledAmount < 0 || walletCreditApplied < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentSettledAmount), "A funded amount cannot be negative.");
        }

        decimal paymentRefunded = 0m;
        decimal walletRefunded = 0m;
        foreach (var refund in priorRefunds)
        {
            if (refund.Status == RefundStatus.Failed)
            {
                continue;
            }

            if (refund.FundingSource == RefundFundingSource.Wallet)
            {
                walletRefunded += refund.Amount;
            }
            else
            {
                paymentRefunded += refund.Amount;
            }
        }

        return new Remaining(
            Math.Max(0m, paymentSettledAmount - paymentRefunded),
            Math.Max(0m, walletCreditApplied - walletRefunded));
    }

    /// <summary>
    /// Splits <paramref name="amount"/> across the two sources, drawing the
    /// gateway payment down first. Deliberate: whatever a cancellation fee
    /// withholds therefore comes off the wallet portion last, so the customer
    /// gets back as much real money as the fee allows and keeps the platform
    /// scrip only for the shortfall. It also leaves every gateway-only
    /// booking - the overwhelming majority - allocating exactly as it did
    /// before wallet funding was refundable at all.
    /// </summary>
    public static Allocation Allocate(decimal amount, Remaining remaining)
    {
        ArgumentNullException.ThrowIfNull(remaining);

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Refund amount must be positive.");
        }

        if (amount > remaining.Total)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Refund amount exceeds what remains refundable on this booking.");
        }

        decimal fromPayment = Math.Min(amount, remaining.PaymentFunded);
        return new Allocation(fromPayment, amount - fromPayment);
    }
}
