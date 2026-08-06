using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Tasks 246/247/248: automatic provider assignment, including the
/// rejection-retry chain and the <see cref="AutoAssignmentOptions.Enabled"/>
/// kill switch (PROVIDER.md OPEN DECISIONS - AUTOMATIC ASSIGNMENT). A fourth
/// independent handler on
/// <see cref="BookingStatusChangedEvent"/>, same shape as
/// <see cref="BookingNotificationTriggerHandler"/>/<see cref="ReferralQualifyingBookingHandler"/>
/// - reacts on its own rather than any of them knowing about the others.
///
/// Fires on every transition to <see cref="BookingStatus.AwaitingFulfilment"/>
/// (decision 4: after payment, not <see cref="BookingStatus.PaymentPending"/>
/// - task 240 established a PaymentPending booking may never be paid for at
/// all) - which covers both a fresh booking (task 246) and a rejection
/// (task 247) identically, since <c>BookingProviderAssignmentService.RejectInternalAsync</c>
/// already re-raises this same event when it returns a booking to
/// AwaitingFulfilment. No new coupling between that service and this handler
/// was needed for the retry chain - the existing event wiring already
/// delivers it; this handler only had to start reading the booking's own
/// assignment history to know it is a retry rather than a first attempt.
///
/// Ranks candidates via <see cref="IProviderMatchingService"/> (task 244, by
/// real road travel time since task 267),
/// excluding every provider with a Rejected row against this booking
/// (regardless of who assigned them - a rejection means "this provider said
/// no to this booking," full stop). Stops after <see cref="AutoAssignmentOptions.RetryAttempts"/>
/// system-attributed rejections and leaves the booking for the manual queue
/// rather than retrying forever (decision 6). Walks the ranked list applying
/// <see cref="IProviderAssignmentEligibilityService"/> (task 245), and
/// assigns the first eligible one via
/// <see cref="IBookingProviderAssignmentService.AssignBySystemAsync"/>. No
/// eligible candidate is not an error (decision 5): the booking simply stays
/// AwaitingFulfilment, exactly where it already sits today for today's
/// unchanged manual admin queue to pick up.
/// </summary>
public sealed class ProviderAutoAssignmentHandler : INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>
{
    private readonly IProviderMatchingService _matchingService;
    private readonly IProviderAssignmentEligibilityService _eligibilityService;
    private readonly IBookingProviderAssignmentService _assignmentService;
    private readonly IBookingProviderAssignmentRepository _assignmentRepository;
    private readonly IOptions<AutoAssignmentOptions> _options;
    private readonly ILogger<ProviderAutoAssignmentHandler> _logger;

    public ProviderAutoAssignmentHandler(
        IProviderMatchingService matchingService,
        IProviderAssignmentEligibilityService eligibilityService,
        IBookingProviderAssignmentService assignmentService,
        IBookingProviderAssignmentRepository assignmentRepository,
        IOptions<AutoAssignmentOptions> options,
        ILogger<ProviderAutoAssignmentHandler> logger)
    {
        _matchingService = matchingService;
        _eligibilityService = eligibilityService;
        _assignmentService = assignmentService;
        _assignmentRepository = assignmentRepository;
        _options = options;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<BookingStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        if (domainEvent.ToStatus != BookingStatus.AwaitingFulfilment)
        {
            return;
        }

        if (!_options.Value.Enabled)
        {
            // Task 248's kill switch - no logging noise on the (expected to
            // be common) disabled path; ProviderAutoAssignmentHandlerTests
            // covers this branch, and a genuinely surprising "why didn't
            // this booking get auto-assigned" investigation starts from the
            // config, not a log line for every single booking.
            return;
        }

        var history = await _assignmentRepository.ListByBookingAsync(domainEvent.BookingId);

        int systemRejections = history.Count(a =>
            a.AssignedByType == BookingAssignedByType.System && a.Status == BookingProviderAssignmentStatus.Rejected);
        if (systemRejections >= _options.Value.RetryAttempts)
        {
            _logger.LogInformation(
                "Booking {BookingId} hit the auto-assignment retry cap ({RetryAttempts}); leaving for manual admin assignment.",
                domainEvent.BookingId, _options.Value.RetryAttempts);
            return;
        }

        var excludeProviderIds = history
            .Where(a => a.Status == BookingProviderAssignmentStatus.Rejected)
            .Select(a => a.ProviderId)
            .ToList();

        await TryAssignAsync(domainEvent.BookingId, excludeProviderIds, cancellationToken);
    }

    /// <summary>Assigns the first eligible ranked candidate not in <paramref name="excludeProviderIds"/>. Returns whether a provider was assigned, so callers can tell an eligible pick from "nothing left to try."</summary>
    public async Task<bool> TryAssignAsync(Guid bookingId, IReadOnlyCollection<Guid>? excludeProviderIds, CancellationToken cancellationToken = default)
    {
        var candidates = await _matchingService.FindCandidatesAsync(bookingId, excludeProviderIds, cancellationToken);

        foreach (var candidate in candidates)
        {
            if (!await _eligibilityService.IsEligibleAsync(candidate.ProviderId, bookingId))
            {
                continue;
            }

            var result = await _assignmentService.AssignBySystemAsync(bookingId, candidate.ProviderId);
            if (result.IsSuccess)
            {
                // Both figures, because they answer different questions: the
                // km is straight-line (unchanged units, task 243), while the
                // travel time is what actually ordered the list (task 267) -
                // and is null when the ranking fell back to distance.
                _logger.LogInformation(
                    "Auto-assigned provider {ProviderId} to booking {BookingId} ({DistanceKm} km straight line, {TravelDurationSeconds} s by road).",
                    candidate.ProviderId, bookingId, candidate.DistanceKm, candidate.TravelDurationSeconds);
                return true;
            }

            // Lost a race (e.g. the provider was suspended or the booking
            // moved on between the eligibility check and the assign call) -
            // try the next candidate rather than giving up on the whole
            // booking over one contended pick.
            _logger.LogWarning(
                "Auto-assignment of provider {ProviderId} to booking {BookingId} failed ({ErrorCode}); trying the next candidate.",
                candidate.ProviderId, bookingId, result.Error.Code);
        }

        _logger.LogInformation("No eligible provider found to auto-assign booking {BookingId}; left for manual admin assignment.", bookingId);
        return false;
    }
}
