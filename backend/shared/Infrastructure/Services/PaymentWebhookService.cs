using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Nestly.Application;
using Nestly.Application.Abstractions.Observability;
using Nestly.Application.Bookings;
using Nestly.Application.Escrow;
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
///
/// A successful callback also settles two more things in the same
/// transaction (tasks 157-158): the platform's commission on this booking is
/// computed and recorded on the transaction, and the paid amount moves into
/// the platform escrow ledger, where it stays held until the booking is
/// completed or refunded.
/// </summary>
public class PaymentWebhookService : IPaymentWebhookService
{
    private readonly IPaymentTransactionRepository _paymentRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IPaymentGateway _gateway;
    private readonly ICommissionService _commissionService;
    private readonly IEscrowService _escrowService;
    private readonly NestlyDbContext _context;
    private readonly IMetricsService _metricsService;
    private readonly ILogger<PaymentWebhookService> _logger;

    public PaymentWebhookService(
        IPaymentTransactionRepository paymentRepository,
        IBookingRepository bookingRepository,
        IServiceRepository serviceRepository,
        IPaymentGateway gateway,
        ICommissionService commissionService,
        IEscrowService escrowService,
        NestlyDbContext context,
        IMetricsService metricsService,
        ILogger<PaymentWebhookService> logger)
    {
        _paymentRepository = paymentRepository;
        _bookingRepository = bookingRepository;
        _serviceRepository = serviceRepository;
        _gateway = gateway;
        _commissionService = commissionService;
        _escrowService = escrowService;
        _context = context;
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task<Result> HandleCallbackAsync(PaymentWebhookRequest request)
    {
        // Task 137a: measures the whole callback-processing path below,
        // including the DB transaction commit - the latency a real payment
        // failure/success takes to become durable, not just gateway/signature
        // checks. Only recorded on the two branches that produce a genuine
        // payment outcome (succeeded/failed inside the try block) - an
        // invalid signature or an already-resolved duplicate redelivery is
        // not a new payment outcome, so neither should skew the metric.
        var stopwatch = Stopwatch.StartNew();

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

                // Task 157: compute and record the platform's commission for
                // this settlement, resolving a per-category override when
                // the booking's (single, today) service has one configured.
                Guid? categoryId = null;
                var firstItem = booking.Items.FirstOrDefault();
                if (firstItem is not null)
                {
                    var service = await _serviceRepository.GetByIdAsync(firstItem.ServiceId);
                    categoryId = service?.CategoryId;
                }

                var commission = _commissionService.Calculate(transaction.Amount, categoryId);
                transaction.RecordCommission(commission.RatePercentage, commission.CommissionAmount);

                await _paymentRepository.UpdateAsync(transaction);
                await _bookingRepository.UpdateAsync(booking);

                // Task 158: hold the paid amount in the platform escrow
                // ledger until the booking is completed or refunded.
                await _escrowService.HoldAsync(booking.Id, transaction.Id, transaction.Amount);
            }
            else
            {
                transaction.MarkAttemptFailed(attempt.Id, request.Status);
                booking.TransitionTo(BookingStatus.PaymentFailed, "Payment failed.");

                await _paymentRepository.UpdateAsync(transaction);
                await _bookingRepository.UpdateAsync(booking);
            }

            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }

        stopwatch.Stop();
        _metricsService.RecordPaymentOutcome(succeeded, stopwatch.Elapsed, succeeded ? null : request.Status);

        return Result.Success();
    }
}
