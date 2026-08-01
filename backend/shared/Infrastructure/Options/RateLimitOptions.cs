namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "RateLimiting" configuration section
/// (SRS 11.2.2 login throttling, SRS 26 abuse control).
///
/// These are per-environment by nature — an automated end-to-end suite or a
/// load test drives far more traffic from one address than a real customer
/// ever does, and a limit baked into source cannot be relaxed for those
/// without shipping different code. The defaults below are the production
/// values, so an environment that says nothing gets the strict behaviour.
/// </summary>
public class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Applies to every endpoint that sends an OTP.</summary>
    public PolicyOptions Otp { get; set; } = new() { PermitLimit = 5, WindowMinutes = 60 };

    /// <summary>Applies to the credential-checking endpoints.</summary>
    public PolicyOptions Login { get; set; } = new() { PermitLimit = 10, WindowMinutes = 15 };

    /// <summary>
    /// Applies to the public, unauthenticated catalog search endpoint (task
    /// 134, SRS 28.1). Sized for legitimate search-as-you-type usage - a
    /// single browsing session firing one request per keystroke can easily
    /// exceed a per-minute handful, so this is intentionally generous
    /// compared to Login/Otp, which gate credential checks rather than reads.
    /// </summary>
    public PolicyOptions Search { get; set; } = new() { PermitLimit = 60, WindowMinutes = 1 };

    /// <summary>
    /// Applies to customer-initiated payment endpoints (order creation and
    /// the sandbox simulate convenience) - task 134, SRS 28.1/28.3 "payment
    /// callback abuse". A genuine customer creates at most a handful of
    /// payment orders per booking (initial attempt plus retries after a
    /// declined payment); this bounds card-testing/fraud-probing abuse of
    /// the same endpoint without a real gateway's own fraud controls in
    /// front of it.
    /// </summary>
    public PolicyOptions Payment { get; set; } = new() { PermitLimit = 20, WindowMinutes = 15 };

    /// <summary>
    /// Applies to the gateway's payment webhook (task 134, SRS 28.3 "payment
    /// callback abuse"). The webhook already authenticates every request by
    /// HMAC signature (see <c>SandboxPaymentGateway.VerifyWebhookSignature</c>)
    /// and is idempotent, so this is defense-in-depth against a flood of
    /// invalid-signature requests rather than the primary control - sized
    /// well above genuine gateway callback volume so legitimate redeliveries
    /// during a settlement burst are never dropped.
    /// </summary>
    public PolicyOptions PaymentWebhook { get; set; } = new() { PermitLimit = 120, WindowMinutes = 1 };

    public class PolicyOptions
    {
        /// <summary>Requests allowed per window, per client IP.</summary>
        public int PermitLimit { get; set; }

        public int WindowMinutes { get; set; }
    }
}
