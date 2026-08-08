namespace Nestly.Application.ProviderManagement;

/// <summary>
/// Task 297: the one composition of <see cref="IProviderMatchingService"/>'s
/// ranking and <see cref="IProviderAssignmentEligibilityService"/>'s
/// per-candidate gate - "the providers who could take THIS booking, best
/// first".
///
/// Extracted because a second caller appeared. That walk used to live inside
/// <c>ProviderAutoAssignmentHandler.TryAssignAsync</c>, and the recurring
/// generator needs the identical answer when the plan's standing provider
/// cannot serve a date; copying the loop would have meant two matchers that
/// could disagree about who is assignable, which is exactly the failure mode
/// the row's "rather than a separate code path" wording is about.
///
/// Lazy on purpose: <see cref="IProviderAssignmentEligibilityService.IsEligibleAsync"/>
/// can cost a billed route lookup per candidate (task 289), so a caller that
/// stops at the first acceptable provider must not have paid for the rest of
/// the list.
/// </summary>
public interface IEligibleProviderSearchService
{
    /// <summary>
    /// Ranked candidates that pass the eligibility gate, streamed best-first.
    /// <paramref name="excludeProviderIds"/> is passed straight through to
    /// <see cref="IProviderMatchingService.FindCandidatesAsync"/> (the
    /// rejection-retry exclusion list, or - task 297 - the standing provider
    /// already known to be unavailable).
    /// </summary>
    IAsyncEnumerable<ProviderMatchCandidate> FindEligibleAsync(
        Guid bookingId,
        IReadOnlyCollection<Guid>? excludeProviderIds = null,
        CancellationToken cancellationToken = default);
}
