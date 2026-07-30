using Microsoft.Extensions.Logging;
using Nestly.Application.Bookings;
using Nestly.Application.Payments;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Payment callback handling (SRS 30.1, 11.11.3, tasks 69a-c): verifies the
/// gateway's signature, applies the outcome idempotently, and keeps the
/// booking-payment mapping in sync. Depends on <see cref="NestlyDbContext"/>
/// directly (unlike most Infrastructure services, which only see it through
/// a repository) because this is the one payment operation that must commit
/// two different aggregates - the PaymentTransaction and the Booking - as a
/// single atomic unit: a crash between the two SaveChanges calls a plain
/// repository-per-aggregate approach would make must never leave a booking
/// Confirmed with no successful payment recorded, or vice versa.
/// </summary>
public class PaymentWebhookService : IPaymentWebhookService
{
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentGateway _gateway;
    private readonly NestlyDbContext _context;
    private readonly ILogger<PaymentWebhookService> _logger;

    public PaymentWebhookService(
        IPaymentTransactionRepository paymentRepository,
        IBookingRepository bookingRepository,
        IPaymentGateway gateway,
        NestlyDbContext context,
        ILogger<PaymentWebhookService> logger)
    {
        _paymentRepository = paymentRepository;
        _bookingRepository = bookingRepository;
        _gateway = gateway;
        _context = context;
        _logger = logger;
    }

    public async Task<Result> HandleCallbackAsync(PaymentWebhookRequest request)
    {
        string canonicalPayload = PaymentWebhookPayload.Build(request.GatewayOrderId, request.GatewayPaymentRef, request.Status);
        if (!_gateway.VerifyWebhookSignature(canonicalPayload, request.Signature))
        {
            // SRS 28.3 "payment callback abuse" - an unsigned/mis-signed
            // callback never touches any state below this point.
            _logger.LogWarning("Rejected a payment webhook with an invalid signature for gateway order {GatewayOrderId}.", request.GatewayOrderId);
            return Result.Failure(Error.Unauthorized("Payment.InvalidWebhookSignature", "The webhook signature could not be verified."));
        }

        var transaction = await _paymentRepository.GetByGatewayOrderIdAsync(request.GatewayOrderId);
        if (transaction is null)
        {
            return Result.Failure(Error.NotFound("Payment.OrderNotFound", "No payment attempt exists for this gateway order."));
        }

        var attempt = transaction.Attempts.Single(a => a.GatewayOrderId == request.GatewayOrderId);

        if (attempt.Status != PaymentAttemptStatus.Created)
        {
            // Idempotent duplicate handling (task 69b): this attempt was
            // already resolved by an earlier callback (the gateway is free
            // to redeliver). The first resolution always wins - a duplicate
            // is a no-op success, never re-applied, even if the redelivery
            // reports a different outcome than the first one did.
            _logger.LogInformation(
                "Ignored a duplicate payment webhook for gateway order {GatewayOrderId} (attempt already {Status}).",
                request.GatewayOrderId, attempt.Status);
            return Result.Success();
        }

        bool succeeded = string.Equals(request.Status, PaymentWebhookPayload.SuccessStatus, StringComparison.OrdinalIgnoreCase);

        var booking = await _bookingRepository.GetByIdAsync(transaction.BookingId);
        if (booking is null)
        {
            // Data-integrity failure, not a business outcome - the FK from
            // payment_transaction to booking is Restrict, so this should be
            // unreachable in practice.
            throw new InvalidOperationException($"Booking {transaction.BookingId} referenced by payment transaction {transaction.Id} was not found.");
        }

        await using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (succeeded)
            {
                transaction.MarkAttemptSucceeded(attempt.Id, request.GatewayPaymentRef);
                booking.TransitionTo(BookingStatus.Confirmed, "Payment succeeded.");
            }
            else
            {
                transaction.MarkAttemptFailed(attempt.Id, request.Status);
                booking.TransitionTo(BookingStatus.PaymentFailed, "Payment failed.");
            }

            await _paymentRepository.UpdateAsync(transaction);
            await _bookingRepository.UpdateAsync(booking);

            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }

        return Result.Success();
    }
}
