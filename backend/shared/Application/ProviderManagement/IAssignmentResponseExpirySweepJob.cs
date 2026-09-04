namespace Nestly.Application.ProviderManagement;

/// <summary>
/// The assignment-response-expiry sweep: finds every system-assigned
/// <see cref="Nestly.Domain.BookingProviderAssignment"/> still <c>Assigned</c>
/// past its <see cref="Nestly.Domain.BookingProviderAssignment.ResponseDeadline"/>
/// with no response, and expires it - the automated counterpart to a
/// provider's own explicit <c>RejectAsync</c>/<c>RejectByProviderAsync</c>.
/// Registered as a Hangfire recurring job (see admin-api <c>Program.cs</c>),
/// same convention as <c>IBookingExpirySweepJob</c> - the interface lives in
/// Application so the job can be resolved through DI/Hangfire's activator.
/// </summary>
public interface IAssignmentResponseExpirySweepJob
{
    /// <summary>Idempotent and safe to re-run (Hangfire's retry convention requires this) - an assignment already moved on (accepted, rejected, superseded) by the time this runs again is simply not picked up.</summary>
    Task SweepAsync(CancellationToken cancellationToken = default);
}
