using MediatR;
using Microsoft.Extensions.Logging;
using Nestly.Application.Amc;
using Nestly.Application.Bookings;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Draws down one unit of AMC entitlement the moment its redemption booking
/// reaches <see cref="BookingStatus.Completed"/> (docs/AMC.md "HOW IT WORKS" -
/// entitlement decrements on completion, not on booking creation, the same
/// principle every other credit-consuming flow in this codebase follows: a
/// cancelled-before-completion visit must not cost the customer an
/// entitlement). Wired the same way <see cref="EscrowReleaseOnCompletionHandler"/>
/// reacts to the identical event - whatever eventually drives a booking to
/// Completed gets this settlement for free without needing to know the AMC
/// module exists.
///
/// This is the ONLY place <see cref="CustomerAmcContract.RedeemVisit"/> is
/// called. <c>IAmcCustomerService.RedeemVisitAsync</c> only creates the
/// zero-priced booking and links it to the contract; it never decrements
/// entitlement itself.
/// </summary>
public sealed class AmcVisitOnBookingCompletionHandler : INotificationHandler<DomainEventNotification<BookingStatusChangedEvent>>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ICustomerAmcContractRepository _contractRepository;
    private readonly IAmcServiceVisitRepository _visitRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AmcVisitOnBookingCompletionHandler> _logger;

    public AmcVisitOnBookingCompletionHandler(
        IBookingRepository bookingRepository,
        ICustomerAmcContractRepository contractRepository,
        IAmcServiceVisitRepository visitRepository,
        TimeProvider timeProvider,
        ILogger<AmcVisitOnBookingCompletionHandler> logger)
    {
        _bookingRepository = bookingRepository;
        _contractRepository = contractRepository;
        _visitRepository = visitRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<BookingStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        if (domainEvent.ToStatus != BookingStatus.Completed)
        {
            return;
        }

        var booking = await _bookingRepository.GetByIdAsync(domainEvent.BookingId);
        if (booking?.AmcContractId is not { } contractId)
        {
            // The overwhelming majority of completed bookings are not AMC
            // redemptions - nothing owed.
            return;
        }

        var contract = await _contractRepository.GetByIdAsync(contractId);
        if (contract is null)
        {
            // Data-integrity gap, not a business outcome - a booking cannot
            // legally carry an AmcContractId that never existed. Logged
            // rather than thrown: this runs as a fire-and-forget domain event
            // handler, not inside the caller's own unit of work.
            _logger.LogWarning(
                "Booking {BookingId} completed with AmcContractId {ContractId}, but no such contract exists - skipping entitlement redemption.",
                domainEvent.BookingId, contractId);
            return;
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            contract.RedeemVisit(domainEvent.BookingId, nowUtc);
        }
        catch (InvalidOperationException ex)
        {
            // The contract was already Exhausted/Expired/Cancelled by the
            // time this booking completed, or its term has since ended -
            // another data-integrity gap (redemption should have been
            // blocked by CanRedeem before the booking was ever created), not
            // something to fail the booking-completion request over.
            _logger.LogWarning(
                ex,
                "Booking {BookingId} completed for AMC contract {ContractId}, but entitlement redemption was rejected: {Message}",
                domainEvent.BookingId, contractId, ex.Message);
            return;
        }

        await _contractRepository.UpdateAsync(contract);
        await _visitRepository.AddAsync(new AmcServiceVisit(Guid.NewGuid(), contract.Id, domainEvent.BookingId, nowUtc));
    }
}
