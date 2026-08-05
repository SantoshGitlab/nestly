using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.Bookings;
using Nestly.Application.Slots;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IBookingExpirySweepJob"/>.</summary>
public class BookingExpirySweepJob : IBookingExpirySweepJob
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ISlotAvailabilityService _slotAvailabilityService;
    private readonly IOptions<BookingExpiryOptions> _options;
    private readonly ILogger<BookingExpirySweepJob> _logger;

    public BookingExpirySweepJob(
        IBookingRepository bookingRepository,
        ISlotAvailabilityService slotAvailabilityService,
        IOptions<BookingExpiryOptions> options,
        ILogger<BookingExpirySweepJob> logger)
    {
        _bookingRepository = bookingRepository;
        _slotAvailabilityService = slotAvailabilityService;
        _options = options;
        _logger = logger;
    }

    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        var cutoffUtc = DateTime.UtcNow.AddMinutes(-_options.Value.ExpiryMinutes);
        var stale = await _bookingRepository.ListStalePaymentPendingAsync(cutoffUtc);

        foreach (var booking in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();

            booking.TransitionTo(BookingStatus.Expired, "Payment was not completed within the expiry window.");
            await _bookingRepository.UpdateAsync(booking);

            // Hand the slot's seat back to the pool, same as
            // CancellationService.ExecuteCancellationAsync - the reservation
            // was taken when the booking was created (BookingService.CreateAsync)
            // and nothing else ever releases it for an abandoned PaymentPending
            // booking.
            await _slotAvailabilityService.ReleaseSlotAsync(booking.SlotWindowId, booking.SlotDate);
        }

        _logger.LogInformation("Booking expiry sweep: {ExpiredCount} stale PaymentPending booking(s) expired.", stale.Count);
    }
}
