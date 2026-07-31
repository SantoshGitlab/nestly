namespace Nestly.Domain;

/// <summary>
/// Pure cancellation fee/refund math (task 80b, SRS 11.14.1). Mirrors
/// <see cref="CommissionCalculator"/>'s split: policy *resolution* (which
/// window/percentage applies) lives in Infrastructure's
/// CancellationPolicyOptions/ICancellationService, since that needs
/// configuration binding this layer must not depend on; this class only
/// turns an already-resolved policy + timing into a rupee-and-paise
/// fee/refund split, so the arithmetic can be unit tested with no DI/config
/// plumbing at all.
/// </summary>
public static class CancellationFeeCalculator
{
    /// <summary>
    /// The fee/refund outcome of a cancellation. <see cref="WithinFreeWindow"/>
    /// is true when the cancellation happens far enough before the booked
    /// slot to owe no fee at all (SRS 11.14.1 "time remaining before slot").
    /// </summary>
    public sealed record Outcome(bool WithinFreeWindow, decimal FeeAmount, decimal RefundAmount);

    /// <summary>
    /// Computes the fee/refund split for a cancellation.
    /// </summary>
    /// <param name="payableAmount">
    /// What the customer actually paid and stands to have refunded (0 if the
    /// booking was never paid for - e.g. still Initiated/PaymentPending).
    /// </param>
    /// <param name="timeUntilSlot">
    /// Booked slot start minus now. Negative values (cancelling after the
    /// slot's start time) are treated the same as any other late
    /// cancellation - still charged the late fee, not a special case.
    /// </param>
    /// <param name="freeCancellationWindowHours">
    /// Cancelling at least this many hours before the slot owes no fee.
    /// </param>
    /// <param name="lateCancellationFeePercentage">
    /// Percentage of <paramref name="payableAmount"/> charged as a fee when
    /// cancelling inside the free window (0-100).
    /// </param>
    public static Outcome Compute(
        decimal payableAmount,
        TimeSpan timeUntilSlot,
        decimal freeCancellationWindowHours,
        decimal lateCancellationFeePercentage)
    {
        if (payableAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payableAmount), "Payable amount cannot be negative.");
        }

        if (freeCancellationWindowHours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(freeCancellationWindowHours), "Free cancellation window cannot be negative.");
        }

        if (lateCancellationFeePercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(lateCancellationFeePercentage), "Late cancellation fee percentage must be between 0 and 100.");
        }

        bool withinFreeWindow = timeUntilSlot.TotalHours >= (double)freeCancellationWindowHours;

        // Nothing was ever paid (booking cancelled before/instead of
        // payment) - there is no fee to charge and nothing to refund,
        // regardless of timing.
        if (payableAmount == 0)
        {
            return new Outcome(withinFreeWindow, 0m, 0m);
        }

        if (withinFreeWindow)
        {
            return new Outcome(true, 0m, payableAmount);
        }

        decimal fee = Math.Round(payableAmount * lateCancellationFeePercentage / 100m, 2, MidpointRounding.ToEven);
        fee = Math.Min(fee, payableAmount);
        decimal refund = payableAmount - fee;
        return new Outcome(false, fee, refund);
    }
}
