namespace Nestly.Application.Payments;

/// <summary>
/// Vendor-agnostic payment gateway capability (SRS 30.1): create order,
/// verify a callback's signature, and refund. There is no mandated real
/// vendor for this project (no Razorpay/Stripe credentials exist) - the only
/// implementation is <c>SandboxPaymentGateway</c>, which simulates a real
/// gateway's behaviour deterministically. A production implementation would
/// satisfy this same interface without any caller needing to change.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Creates an order/intent for the given amount (SRS 30.1 "create payment order", task 68a).</summary>
    Task<GatewayOrderResult> CreateOrderAsync(GatewayCreateOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Issues a refund against a previously captured payment (SRS 30.1 "refund API", task 75b/c).</summary>
    Task<GatewayRefundResult> RefundAsync(GatewayRefundRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that <paramref name="signature"/> is a valid signature of
    /// <paramref name="canonicalPayload"/> (SRS 30.1 "webhook/callback
    /// support"; SRS 28.3 "payment callback abuse") - HMAC-style, computed
    /// against a shared secret the caller never sees.
    /// </summary>
    bool VerifyWebhookSignature(string canonicalPayload, string signature);

    /// <summary>
    /// Signs a payload the same way a real gateway would sign its outbound
    /// callback. Only meaningful for the sandbox: since there is no real
    /// gateway to call our webhook, the sandbox "simulate" endpoint uses this
    /// to construct a callback payload with a valid signature, so the actual
    /// webhook handler - <see cref="VerifyWebhookSignature"/> and everything
    /// downstream of it - is exercised for real rather than bypassed.
    /// </summary>
    string SignPayload(string canonicalPayload);
}

/// <summary><paramref name="Receipt"/> is an opaque merchant reference (the booking id) the gateway echoes back, not used for lookups.</summary>
public sealed record GatewayCreateOrderRequest(Guid BookingId, decimal Amount, string Currency, string Receipt);

public sealed record GatewayOrderResult(string GatewayOrderId, string Status);

public sealed record GatewayRefundRequest(string GatewayPaymentRef, decimal Amount, string Currency, string Receipt);

public sealed record GatewayRefundResult(string GatewayRefundId, string Status);
