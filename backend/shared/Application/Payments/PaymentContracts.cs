using Nestly.Domain;

namespace Nestly.Application.Payments;

/// <summary>
/// Requests a gateway order for a booking (task 68a-d). <paramref name="IdempotencyKey"/>
/// is optional - when the caller omits it, dedup still happens on
/// <see cref="BookingId"/> alone (a booking can have at most one payment
/// transaction), but a client that generates its own key gets an extra guard
/// against, e.g., a doubled button-tap creating two HTTP requests before the
/// first one's response comes back.
/// </summary>
public record CreatePaymentOrderRequest(Guid BookingId, string? IdempotencyKey);

/// <summary>The order to hand to the client-side checkout (sandbox or real gateway SDK).</summary>
public record PaymentOrderResponse(
    Guid PaymentTransactionId,
    Guid AttemptId,
    string GatewayOrderId,
    decimal Amount,
    string Currency,
    int AttemptNumber,
    DateTime CreatedAtUtc);

public record PaymentAttemptResponse(
    Guid Id,
    int AttemptNumber,
    string GatewayOrderId,
    string? GatewayPaymentRef,
    PaymentAttemptStatus Status,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);

/// <summary>Full transaction detail (SRS 11.11.3 "payment transaction history shall be available to admin"; SRS 14.3 reconciliation, task 71).</summary>
public record PaymentTransactionResponse(
    Guid Id,
    Guid BookingId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    PaymentTransactionStatus Status,
    IReadOnlyList<PaymentAttemptResponse> Attempts,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// The sandbox's webhook callback shape (task 69a). <paramref name="Signature"/>
/// is an HMAC-SHA256 of the other fields joined canonically (see
/// <c>SandboxPaymentGateway.BuildCanonicalPayload</c>), computed against a
/// shared secret - mirrors how a real gateway signs its callback so the
/// verification code path is genuine, not a sandbox-only shortcut.
/// </summary>
public record PaymentWebhookRequest(string GatewayOrderId, string GatewayPaymentRef, string Status, string Signature);

/// <summary>
/// Sandbox-only convenience: since no real gateway exists to complete a
/// payment and call our webhook, this simulates that round trip for a given
/// order (task 68b). Internally it builds and signs the exact same payload
/// <see cref="PaymentWebhookRequest"/> carries and runs it through the real
/// webhook handler - nothing about verification or idempotency is skipped.
/// </summary>
public record SimulatePaymentRequest(string GatewayOrderId);
