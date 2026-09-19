namespace Nestly.Application.Bookings;

/// <summary>
/// Recurring-booking payment-timing fix: attempts off-session payment for
/// recurring occurrences whose plan has opted in to auto-charge, through the
/// same sandbox gateway seam <c>SubscriptionBillingJob</c> already uses
/// (<c>IPaymentGateway</c>/<c>ISandboxPaymentSimulator</c> via
/// <c>IPaymentService.CreateOrderAsync</c>/<c>SimulateAsync</c>) - not a
/// second, invented payment integration.
/// </summary>
public interface IRecurringOccurrenceAutoChargeJob
{
    /// <summary>Idempotent and safe to re-run (Hangfire's retry convention requires this) - a booking already moved out of PaymentPending (by a successful charge, a manual payment, or BookingExpirySweepJob) is simply not picked up again.</summary>
    Task ProcessDueAttemptsAsync(CancellationToken cancellationToken = default);
}
