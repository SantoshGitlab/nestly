using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// The single payment record for one booking (SRS 23.4 payment_transaction,
/// SRS 11.11.3 "booking-payment mapping shall be preserved"). A booking has
/// at most one of these ever - retries (task 70) add new
/// <see cref="PaymentAttempt"/> rows underneath the same transaction rather
/// than creating a new one, which is what keeps the mapping stable across a
/// failed-then-retried payment.
/// </summary>
public class PaymentTransaction : AggregateRoot<Guid>
{
    private readonly List<PaymentAttempt> _attempts = [];

    public Guid BookingId { get; private set; }

    public Guid CustomerId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "INR";

    public PaymentTransactionStatus Status { get; private set; }

    /// <summary>
    /// Caller-supplied (or server-generated, when the caller sends none)
    /// deduplication key for the "create order" request itself (task 68d) -
    /// distinct from webhook idempotency (task 69b), which is handled by
    /// <see cref="PaymentAttempt"/>'s own status guard.
    /// </summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyList<PaymentAttempt> Attempts => _attempts;

    /// <summary>The attempt currently in flight or most recently resolved.</summary>
    public PaymentAttempt? LatestAttempt => _attempts.OrderByDescending(a => a.AttemptNumber).FirstOrDefault();

    protected PaymentTransaction() { }

    public PaymentTransaction(Guid id, Guid bookingId, Guid customerId, decimal amount, string currency, string idempotencyKey)
        : base(id)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");
        }

        BookingId = bookingId;
        CustomerId = customerId;
        Amount = amount;
        Currency = currency ?? throw new ArgumentException("Currency is required.", nameof(currency));
        IdempotencyKey = idempotencyKey ?? throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        Status = PaymentTransactionStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    /// <summary>
    /// Starts a new gateway order round-trip: the first attempt on creation,
    /// or a retry (task 70) after a prior one failed. Preserves booking
    /// intent by reusing this same transaction/mapping rather than minting a
    /// new one.
    /// </summary>
    public PaymentAttempt StartAttempt(Guid attemptId, string gatewayOrderId)
    {
        if (Status == PaymentTransactionStatus.Success)
        {
            throw new InvalidOperationException("This booking has already been paid for.");
        }

        var attempt = new PaymentAttempt(attemptId, Id, _attempts.Count + 1, gatewayOrderId);
        _attempts.Add(attempt);
        Status = PaymentTransactionStatus.Pending;
        UpdatedAtUtc = DateTime.UtcNow;
        return attempt;
    }

    public void MarkAttemptSucceeded(Guid attemptId, string gatewayPaymentRef)
    {
        var attempt = _attempts.SingleOrDefault(a => a.Id == attemptId)
            ?? throw new InvalidOperationException($"Payment attempt {attemptId} does not belong to this transaction.");

        attempt.MarkSucceeded(gatewayPaymentRef);
        Status = PaymentTransactionStatus.Success;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAttemptFailed(Guid attemptId, string reason)
    {
        var attempt = _attempts.SingleOrDefault(a => a.Id == attemptId)
            ?? throw new InvalidOperationException($"Payment attempt {attemptId} does not belong to this transaction.");

        attempt.MarkFailed(reason);
        Status = PaymentTransactionStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
