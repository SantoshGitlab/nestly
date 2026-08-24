namespace Nestly.Application.ProviderManagement;

/// <summary>
/// The provider-queue model's one-active-job rule: a provider may hold
/// several <em>accepted</em> future jobs at once (their day's queue), but may
/// only ever be actively working one - <c>ProviderEnRoute</c>,
/// <c>ProviderArrived</c> or <c>InProgress</c> - at a time. Enforced at the
/// moment a job is <em>activated</em> (en-route or started), not at accept
/// time, since holding a queue of accepted-but-not-yet-started jobs is the
/// entire point of the model.
/// </summary>
public interface IProviderActiveJobLimitService
{
    /// <summary>Whether this provider has a different job - not <paramref name="excludingBookingId"/> - currently in an active fulfilment state.</summary>
    Task<bool> HasAnotherActiveJobAsync(Guid providerId, Guid excludingBookingId, CancellationToken cancellationToken = default);
}
