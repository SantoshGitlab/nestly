using MediatR;
using Microsoft.Extensions.Logging;
using Nestly.Application.Bookings;
using Nestly.Application.ProviderReferral;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// When a booking reaches Completed, checks for a pending provider referral
/// keyed by the assigned provider (the referee) and, once that provider has
/// reached the configured completed-job count, marks it Qualified and
/// immediately disburses the reward. Mirrors ReferralQualifyingBookingHandler
/// structurally - a fourth independent handler on
/// <see cref="BookingStatusChangedEvent"/>, same shape as
/// EscrowReleaseOnCompletionHandler, BookingNotificationTriggerHandler, and
/// ReferralQualifyingBookingHandler itself.
///
/// <para>
/// <b>Why a completed-job count instead of a single qualifying booking</b>
/// (unlike the customer program's single-booking-amount threshold): a
/// provider referral pays out real money for bringing on a new *worker*, and
/// a single completed job is a weak signal that the referee is a genuine,
/// active provider rather than a fabricated account created only to trigger
/// the signup reward. Requiring several completed jobs before either side is
/// paid is the fraud control (PROVIDER-REFERRAL.md "FRAUD / ABUSE PREVENTION").
/// </para>
/// </summary>
public sealed class ProviderReferralQualifyingJobHandler : INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IProviderReferralRepository _referralRepository;
    private readonly IProviderReferralRewardService _rewardService;
    private readonly ILogger<ProviderReferralQualifyingJobHandler> _logger;

    public ProviderReferralQualifyingJobHandler(
        IBookingRepository bookingRepository,
        IProviderReferralRepository referralRepository,
        IProviderReferralRewardService rewardService,
        ILogger<ProviderReferralQualifyingJobHandler> logger)
    {
        _bookingRepository = bookingRepository;
        _referralRepository = referralRepository;
        _rewardService = rewardService;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<BookingStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        if (domainEvent.ToStatus != BookingStatus.Completed)
        {
            return;
        }

        Booking? booking = await _bookingRepository.GetByIdAsync(domainEvent.BookingId);
        if (booking is null)
        {
            _logger.LogWarning("Provider referral qualification check found no booking for id {BookingId}.", domainEvent.BookingId);
            return;
        }

        if (booking.AssignedProviderId is not { } refereeProviderId)
        {
            // No provider assigned - cannot be anyone's referee completion.
            return;
        }

        ProviderReferral? referral = await _referralRepository.GetByRefereeProviderIdAsync(refereeProviderId);
        if (referral is null || referral.Status != ProviderReferralStatus.Registered)
        {
            // No referral, or already qualified/rewarded/expired - the loop
            // only ever fires once per referee, at the completion that first
            // reaches the qualifying count.
            return;
        }

        // +1 for the booking that just completed: BookingStatusChangedEvent
        // fires from within the same unit of work that transitions this
        // booking to Completed, so CountCompletedByAssignedProviderAsync's
        // "excluding" parameter (this booking) has not yet observed it as
        // Completed in a query against already-committed state.
        int completedCount = await _bookingRepository.CountCompletedByAssignedProviderAsync(refereeProviderId, booking.Id) + 1;
        if (completedCount < referral.QualifyingCompletedJobsCount)
        {
            // Not there yet - stays Registered until either a later
            // completion qualifies it or the expiry sweep closes it out.
            return;
        }

        referral.MarkQualified(booking.Id);
        await _referralRepository.UpdateAsync(referral);

        await _rewardService.DisburseAsync(referral);
    }
}
