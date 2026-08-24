namespace Nestly.Application.ProviderManagement;

/// <summary>
/// The provider-queue model's overrun handling: when a job finishes later
/// than its booked slot (a verified-complete, non-duration-based job whose
/// actual finish time ran past <c>Booking.SlotEndTimeSnapshot</c>), the
/// provider is genuinely still occupied longer than assumed when their other
/// same-day accepted-but-not-yet-started jobs were handed to them. This
/// re-checks travel feasibility for those queued jobs against the new,
/// later "free from" instant and withdraws any that are no longer feasible,
/// returning them to the assignable pool - the same outcome a provider-
/// initiated rejection produces (task 159's existing reassignment queue),
/// rather than silently guaranteeing a late arrival.
/// </summary>
public interface IOverrunReassignmentService
{
    /// <summary>
    /// Re-checks this provider's other same-day queued jobs (excluding
    /// <paramref name="completedBookingId"/>, the one that just overran) and
    /// withdraws any that travel feasibility no longer supports.
    /// </summary>
    Task ReassignInfeasibleQueuedJobsAsync(
        Guid providerId, DateOnly slotDate, Guid completedBookingId, CancellationToken cancellationToken = default);
}
