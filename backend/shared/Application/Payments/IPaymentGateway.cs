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
    /// Builds the exact string a callback's signature is computed over, from
    /// the fields a caller (a webhook controller action) extracted out of the
    /// gateway's raw callback. This is vendor-specific - the sandbox's
    /// canonical string is 3 fields joined by "|"; PayU's is a 12-field
    /// reverse-hash sequence - so it lives behind the gateway abstraction
    /// rather than hardcoded in <c>PaymentWebhookService</c>, which only
    /// needs to call <see cref="VerifyWebhookSignature"/> with whatever this
    /// returns.
    /// </summary>
    string BuildCanonicalPayload(PaymentWebhookRequest request);
}

/// <summary>
/// <paramref name="Receipt"/> is an opaque merchant reference (the booking
/// id) the gateway echoes back, not used for lookups. The <c>Customer*</c>
/// fields are optional (default null): the sandbox ignores them entirely,
/// and an off-session caller with no customer contact on hand (e.g.
/// <c>SubscriptionBillingJob</c>) can simply omit them - a real hosted-
/// checkout gateway that needs them (PayU requires firstname/email in its
/// signed hash) falls back to a synthetic placeholder rather than failing.
/// </summary>
public sealed record GatewayCreateOrderRequest(
    Guid BookingId,
    decimal Amount,
    string Currency,
    string Receipt,
    string? CustomerName = null,
    string? CustomerMobile = null,
    string? CustomerEmail = null);

/// <summary>
/// <paramref name="CheckoutRedirectUrl"/>/<paramref name="CheckoutFormFields"/>
/// are populated only by a hosted-checkout-style gateway (the browser must
/// be redirected there to actually pay) - null for the sandbox, which has no
/// real checkout page to redirect to.
/// </summary>
public sealed record GatewayOrderResult(
    string GatewayOrderId,
    string Status,
    string? CheckoutRedirectUrl = null,
    IReadOnlyDictionary<string, string>? CheckoutFormFields = null);

public sealed record GatewayRefundRequest(string GatewayPaymentRef, decimal Amount, string Currency, string Receipt);

/// <summary><paramref name="FailureReason"/> is populated only when <paramref name="Status"/> indicates the refund did not go through - a real gateway's refund can genuinely fail (insufficient balance, already refunded, bank rejection), unlike the sandbox's unconditional success.</summary>
public sealed record GatewayRefundResult(string GatewayRefundId, string Status, string? FailureReason = null);
