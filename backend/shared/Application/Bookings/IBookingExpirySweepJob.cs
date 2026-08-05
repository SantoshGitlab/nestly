namespace Nestly.Application.Bookings;

/// <summary>
/// Task 240's scheduled sweep: expires any booking abandoned in
/// PaymentPending past <c>BookingExpiryOptions.ExpiryMinutes</c> and releases
/// the slot seat it was holding. Registered as a Hangfire recurring job (see
/// <c>BackgroundJobRegistration</c> / admin-api <c>Program.cs</c>) - the
/// interface lives in Application so the job can be resolved through
/// DI/Hangfire's activator the same way <c>IWalletCreditExpirySweepJob</c> is.
/// </summary>
public interface IBookingExpirySweepJob
{
    /// <summary>Idempotent and safe to re-run (Hangfire's retry convention requires this) - a booking already moved out of PaymentPending (by this job or a real payment) is simply not picked up again.</summary>
    Task SweepAsync(CancellationToken cancellationToken = default);
}
