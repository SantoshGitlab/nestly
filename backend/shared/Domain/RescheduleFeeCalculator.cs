namespace Nestly.Domain;

/// <summary>
/// Pure reschedule fee-impact math (task 82d, SRS 11.15.2 "any fee impact").
/// Same split as <see cref="CancellationFeeCalculator"/>: policy resolution
/// lives in Infrastructure's ReschedulePolicyOptions/IRescheduleService, this
/// class only turns an already-resolved policy + timing into a fee amount.
///
/// Unlike cancellation, a reschedule fee is reported/recorded (SRS 11.15.2
/// "data captured") but not collected through a new payment here - there is
/// no partial top-up payment flow in this phase (Phase 5). Collecting it is
/// a natural extension point once one exists.
/// </summary>
public static class RescheduleFeeCalculator
{
    public sealed record Outcome(bool IsLate, decimal FeeAmount);

    /// <param name="payableAmount">The booking's paid amount the fee percentage applies against (0 if never paid for).</param>
    /// <param name="timeUntilSlot">Current slot start minus now.</param>
    /// <param name="lateFeeThresholdHours">Rescheduling with less than this many hours to go incurs a fee.</param>
    /// <param name="lateRescheduleFeePercentage">Percentage of <paramref name="payableAmount"/> charged when late (0-100).</param>
    public static Outcome Compute(
        decimal payableAmount,
        TimeSpan timeUntilSlot,
        decimal lateFeeThresholdHours,
        decimal lateRescheduleFeePercentage)
    {
        if (payableAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payableAmount), "Payable amount cannot be negative.");
        }

        if (lateFeeThresholdHours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lateFeeThresholdHours), "Late fee threshold cannot be negative.");
        }

        if (lateRescheduleFeePercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(lateRescheduleFeePercentage), "Late reschedule fee percentage must be between 0 and 100.");
        }

        bool isLate = timeUntilSlot.TotalHours < (double)lateFeeThresholdHours;
        if (!isLate || payableAmount == 0)
        {
            return new Outcome(isLate, 0m);
        }

        decimal fee = Math.Round(payableAmount * lateRescheduleFeePercentage / 100m, 2, MidpointRounding.ToEven);
        fee = Math.Min(fee, payableAmount);
        return new Outcome(true, fee);
    }
}
