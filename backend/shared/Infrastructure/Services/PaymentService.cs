using Nestly.Application.Bookings;
using Nestly.Application.Payments;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Payment order creation, idempotency/dedup, and retry (SRS 11.11, 30.1,
/// tasks 68a-d, 70). A booking has at most one <see cref="PaymentTransaction"/>
/// ever (SRS 11.11.3 "booking-payment mapping shall be preserved") - both a
/// duplicate request and a retry after failure resolve to the same
/// transaction, just a different attempt underneath it.
/// </summary>
public class PaymentService : IPaymentService
{
    private const string Currency = "INR";

    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentGateway _gateway;

    public PaymentService(IPaymentTransactionRepository paymentRepository, IBookingRepository bookingRepository, IPaymentGateway gateway)
    {
        _paymentRepository = paymentRepository;
        _bookingRepository = bookingRepository;
        _gateway = gateway;
    }

    public async Task<Result<PaymentOrderResponse>> CreateOrderAsync(Guid customerId, CreatePaymentOrderRequest request)
    {
        var booking = await _bookingRepository.GetByIdAsync(request.BookingId);
        if (booking is null || booking.CustomerId != customerId)
        {
            return Error.NotFound("Payment.BookingNotFound", "The specified booking does not exist.");
        }

        if (booking.Status is not (BookingStatus.PaymentPending or BookingStatus.PaymentFailed))
        {
            return Error.Business(
                "Payment.BookingNotPayable",
                $"Booking is in status '{booking.Status}' and cannot accept a payment right now.");
        }

        var existing = await _paymentRepository.GetByBookingIdAsync(booking.Id);

        switch (existing?.Status)
        {
            case PaymentTransactionStatus.Success:
                return Error.Conflict("Payment.AlreadyPaid", "This booking has already been paid for.");

            case PaymentTransactionStatus.Pending:
                // Idempotency/dedup (task 68d): an attempt is already in
                // flight for this booking - hand back that same order rather
                // than creating a second one from a duplicate request.
                return Result.Success(ToOrderResponse(existing, existing.LatestAttempt!));

            case PaymentTransactionStatus.Cancelled:
                return Error.Business("Payment.TransactionCancelled", "This booking's payment was cancelled and can no longer be retried.");
        }

        var gatewayResult = await _gateway.CreateOrderAsync(
            new GatewayCreateOrderRequest(booking.Id, booking.TotalPayableSnapshot, Currency, booking.Id.ToString("N")));

        PaymentTransaction transaction;
        if (existing is null)
        {
            transaction = new PaymentTransaction(
                Guid.NewGuid(), booking.Id, customerId, booking.TotalPayableSnapshot, Currency,
                string.IsNullOrWhiteSpace(request.IdempotencyKey) ? Guid.NewGuid().ToString("N") : request.IdempotencyKey);
            transaction.StartAttempt(Guid.NewGuid(), gatewayResult.GatewayOrderId);
            await _paymentRepository.AddAsync(transaction);
        }
        else
        {
            // Retry (task 70): existing.Status is Failed here - reuse the
            // same transaction (preserving the booking-payment mapping and
            // the customer's original booking intent) and just add a new
            // attempt underneath it.
            transaction = existing;
            transaction.StartAttempt(Guid.NewGuid(), gatewayResult.GatewayOrderId);
            await _paymentRepository.UpdateAsync(transaction);
        }

        return Result.Success(ToOrderResponse(transaction, transaction.LatestAttempt!));
    }

    public async Task<Result<PaymentTransactionResponse>> GetByBookingIdAsync(Guid customerId, Guid bookingId)
    {
        var transaction = await _paymentRepository.GetByBookingIdAsync(bookingId);
        if (transaction is null || transaction.CustomerId != customerId)
        {
            return Error.NotFound("Payment.NotFound", "No payment transaction exists for this booking.");
        }

        return Result.Success(ToTransactionResponse(transaction));
    }

    private static PaymentOrderResponse ToOrderResponse(PaymentTransaction transaction, PaymentAttempt attempt) => new(
        transaction.Id, attempt.Id, attempt.GatewayOrderId, transaction.Amount, transaction.Currency, attempt.AttemptNumber, attempt.CreatedAtUtc);

    private static PaymentTransactionResponse ToTransactionResponse(PaymentTransaction transaction) => new(
        transaction.Id,
        transaction.BookingId,
        transaction.CustomerId,
        transaction.Amount,
        transaction.Currency,
        transaction.Status,
        transaction.Attempts
            .OrderBy(a => a.AttemptNumber)
            .Select(a => new PaymentAttemptResponse(a.Id, a.AttemptNumber, a.GatewayOrderId, a.GatewayPaymentRef, a.Status, a.FailureReason, a.CreatedAtUtc, a.CompletedAtUtc))
            .ToList(),
        transaction.CreatedAtUtc,
        transaction.UpdatedAtUtc);
}
