using Nestly.Application.Bookings;
using Nestly.Application.Escrow;
using Nestly.Application.Payments;
using Nestly.Application.Refunds;
using Nestly.Application.Wallet;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Refund initiation and lifecycle (SRS 11.17.2, 14.4, tasks 75a-d). Like
/// <see cref="PaymentWebhookService"/>, this depends on <see cref="NestlyDbContext"/>
/// directly to commit the Booking transition and the RefundTransaction
/// atomically - a refund that "succeeded" but left the booking in the wrong
/// state (or vice versa) is exactly the kind of inconsistency SRS 29.3
/// ("critical workflows should support reconciliation") exists to prevent.
///
/// Also keeps the platform escrow ledger honest (task 158): a refunded
/// booking's hold - if any is still held, i.e. it wasn't already released to
/// a provider on completion - is released back out in the same transaction,
/// without changing how the refund itself is actually paid out (still
/// <see cref="IWalletService"/> or the gateway, exactly as before).
/// </summary>
public class RefundService : IRefundService
{
    private static readonly BookingStatus[] EligibleBookingStatuses =
    [
        BookingStatus.Completed, BookingStatus.CancelledByCustomer, BookingStatus.CancelledByAdmin, BookingStatus.RefundPending
    ];

    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly IRefundTransactionRepository _refundRepository;
    private readonly IWalletService _walletService;
    private readonly IEscrowService _escrowService;
    private readonly IPaymentGateway _gateway;
    private readonly NestlyDbContext _context;

    public RefundService(
        IBookingRepository bookingRepository,
        IPaymentTransactionRepository paymentRepository,
        IRefundTransactionRepository refundRepository,
        IWalletService walletService,
        IEscrowService escrowService,
        IPaymentGateway gateway,
        NestlyDbContext context)
    {
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _refundRepository = refundRepository;
        _walletService = walletService;
        _escrowService = escrowService;
        _gateway = gateway;
        _context = context;
    }

    public Task<Result<RefundTransactionResponse>> InitiateFullRefundAsync(Guid bookingId, string reason, RefundMethod method = RefundMethod.Gateway) =>
        InitiateAsync(bookingId, RefundType.Full, requestedAmount: null, reason, method);

    public Task<Result<RefundTransactionResponse>> InitiatePartialRefundAsync(Guid bookingId, decimal amount, string reason, RefundMethod method = RefundMethod.Gateway)
    {
        if (amount <= 0)
        {
            return Task.FromResult(Result.Failure<RefundTransactionResponse>(Error.Validation("Refund.InvalidAmount", "Refund amount must be positive.")));
        }

        return InitiateAsync(bookingId, RefundType.Partial, amount, reason, method);
    }

    public async Task<Result<IReadOnlyList<RefundTransactionResponse>>> ListByBookingAsync(Guid customerId, Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("Refund.BookingNotFound", "The specified booking does not exist.");
        }

        var refunds = await _refundRepository.ListByBookingAsync(bookingId);
        IReadOnlyList<RefundTransactionResponse> response = refunds.Select(ToResponse).ToList();
        return Result.Success(response);
    }

    private async Task<Result<RefundTransactionResponse>> InitiateAsync(
        Guid bookingId, RefundType type, decimal? requestedAmount, string reason, RefundMethod method)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return Error.NotFound("Refund.BookingNotFound", "The specified booking does not exist.");
        }

        if (!EligibleBookingStatuses.Contains(booking.Status))
        {
            return Error.Business(
                "Refund.BookingNotEligible",
                $"A booking in status '{booking.Status}' is not eligible for a refund.");
        }

        var payment = await _paymentRepository.GetByBookingIdAsync(bookingId);
        if (payment is null || payment.Status != PaymentTransactionStatus.Success)
        {
            return Error.Business("Refund.NoSuccessfulPayment", "This booking has no successful payment to refund.");
        }

        var priorRefunds = await _refundRepository.ListByPaymentTransactionAsync(payment.Id);
        decimal alreadyRefunded = priorRefunds.Where(r => r.Status != RefundStatus.Failed).Sum(r => r.Amount);
        decimal remaining = payment.Amount - alreadyRefunded;

        decimal amount;
        if (type == RefundType.Full)
        {
            if (remaining <= 0)
            {
                return Error.Business("Refund.NothingToRefund", "This payment has already been fully refunded.");
            }

            amount = remaining;
        }
        else
        {
            amount = requestedAmount!.Value;
            if (amount > remaining)
            {
                return Error.Validation("Refund.ExceedsRemainingBalance", $"Only {remaining} remains refundable on this payment.");
            }
        }

        var refund = new RefundTransaction(Guid.NewGuid(), bookingId, payment.Id, type, method, amount, reason);
        refund.MarkProcessing();

        await using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (booking.Status != BookingStatus.RefundPending)
            {
                booking.TransitionTo(BookingStatus.RefundPending, reason);
            }

            if (method == RefundMethod.Wallet)
            {
                await _walletService.CreditAsync(booking.CustomerId, amount, WalletSourceType.Refund, refund.Id, reason);
                refund.MarkRefunded(gatewayRefundRef: null);
            }
            else
            {
                var successfulAttempt = payment.Attempts.First(a => a.Status == PaymentAttemptStatus.Success);
                var gatewayResult = await _gateway.RefundAsync(
                    new GatewayRefundRequest(successfulAttempt.GatewayPaymentRef!, amount, payment.Currency, bookingId.ToString("N")));
                refund.MarkRefunded(gatewayResult.GatewayRefundId);
            }

            if (alreadyRefunded + amount >= payment.Amount)
            {
                booking.TransitionTo(BookingStatus.Refunded, "Refund completed.");

                // Task 310: a booking that consumed wallet balance at
                // checkout gets it back once the payment side is fully
                // settled - the customer never received the service that
                // balance was spent on. Deliberately different from how a
                // Coupon's redemption is treated: RedemptionCount is never
                // decremented on any refund (see CouponService), since a
                // coupon spends the merchant's inventory of a promotional
                // offer, not the customer's own money - clawing it back has
                // no customer-facing harm to undo. Wallet credit is real
                // money the customer will otherwise simply never see again,
                // so it gets the explicit reversal a coupon doesn't need.
                // Scoped to a FULL settlement only (this branch) - a partial
                // refund leaves the booking's wallet spend exactly where a
                // coupon's discount is left on the same partial refund: not
                // prorated back.
                if (booking.WalletCreditAppliedSnapshot is > 0)
                {
                    await _walletService.CreditAsync(
                        booking.CustomerId, booking.WalletCreditAppliedSnapshot.Value, WalletSourceType.BookingWalletCreditReversal,
                        booking.Id, "Wallet credit reversed - booking fully refunded");
                }
            }

            await _refundRepository.AddAsync(refund);
            await _bookingRepository.UpdateAsync(booking);

            // Task 158: release this refund's amount back out of escrow -
            // a no-op if the booking's hold was already released to its
            // provider on completion before this refund was issued.
            await _escrowService.ReleaseForRefundAsync(bookingId, refund.Id, amount);

            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }

        return Result.Success(ToResponse(refund));
    }

    private static RefundTransactionResponse ToResponse(RefundTransaction refund) => new(
        refund.Id, refund.BookingId, refund.PaymentTransactionId, refund.Type, refund.Method,
        refund.Amount, refund.Status, refund.GatewayRefundRef, refund.Reason, refund.CreatedAtUtc, refund.ProcessedAtUtc);
}
