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

    public class PolicyOptions
    {
        /// <summary>Requests allowed per window, per client IP.</summary>
        public int PermitLimit { get; set; }

        public int WindowMinutes { get; set; }
    }
}
