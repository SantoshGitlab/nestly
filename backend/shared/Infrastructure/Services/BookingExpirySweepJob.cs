using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nestly.Application.Bookings;
using Nestly.Application.Coupons;
using Nestly.Application.Slots;
using Nestly.Application.Wallet;
using Nestly.Domain;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="IBookingExpirySweepJob"/>.</summary>
public class BookingExpirySweepJob : IBookingExpirySweepJob
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ISlotAvailabilityService _slotAvailabilityService;
    private readonly IWalletService _walletService;
    private readonly ICouponService _couponService;
    private readonly IOptions<BookingExpiryOptions> _options;
    private readonly IOptions<RecurringBookingOptions> _recurringOptions;
    private readonly ILogger<BookingExpirySweepJob> _logger;

    public BookingExpirySweepJob(
        IBookingRepository bookingRepository,
        ISlotAvailabilityService slotAvailabilityService,
        IWalletService walletService,
        ICouponService couponService,
        IOptions<BookingExpiryOptions> options,
        IOptions<RecurringBookingOptions> recurringOptions,
        ILogger<BookingExpirySweepJob> logger)
    {
        _bookingRepository = bookingRepository;
        _slotAvailabilityService = slotAvailabilityService;
        _walletService = walletService;
        _couponService = couponService;
        _options = options;
        _recurringOptions = recurringOptions;
        _logger = logger;
    }

    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        var cutoffUtc = DateTime.UtcNow.AddMinutes(-_options.Value.ExpiryMinutes);
        // A recurring-generated occurrence gets its own, much longer, cutoff -
        // see RecurringBookingOptions.PaymentWindowHours's doc comment for why
        // the one-off checkout window is the wrong clock for a booking nobody
        // is actively watching.
        var recurringCutoffUtc = DateTime.UtcNow.AddHours(-_recurringOptions.Value.PaymentWindowHours);
        var stale = await _bookingRepository.ListStalePaymentPendingAsync(cutoffUtc, recurringCutoffUtc);

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

            // Same reasoning, for the other two things BookingService.CreateAsync
            // reserves atomically alongside the slot: wallet balance debited at
            // checkout (task 310) and a coupon redemption (task 72a-d). Neither
            // was ever refunded/released for an abandoned PaymentPending
            // booking before this - the customer permanently lost real wallet
            // balance, and a single-use coupon was permanently burned, for an
            // order that never actually happened. CancellationService already
            // gets the wallet half right for a *manual* cancellation (via
            // IRefundService); this mirrors that for the automatic-expiry path,
            // which had neither.
            if (booking.WalletCreditAppliedSnapshot is { } walletAmount && walletAmount > 0)
            {
                await _walletService.CreditAsync(
                    booking.CustomerId, walletAmount, WalletSourceType.BookingWalletCreditReversal, booking.Id,
                    "Wallet credit reversed - booking expired unpaid");
            }

            await _couponService.ReleaseAsync(booking.Id);
        }

        _logger.LogInformation("Booking expiry sweep: {ExpiredCount} stale PaymentPending booking(s) expired.", stale.Count);
    }
}
