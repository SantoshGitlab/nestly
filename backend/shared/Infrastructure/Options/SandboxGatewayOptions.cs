using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "PaymentGateway:Sandbox" configuration
/// section. This project has no real payment vendor credentials (SRS 30.1
/// requires only vendor-agnostic gateway capabilities) - <see cref="WebhookSigningSecret"/>
/// is the shared secret the sandbox gateway signs its own simulated
/// callbacks with, standing in for the secret a real gateway would issue per
/// merchant account.
/// </summary>
public class SandboxGatewayOptions
{
    public const string SectionName = "PaymentGateway:Sandbox";

    [Required, MinLength(16)]
    public string WebhookSigningSecret { get; set; } = string.Empty;
}
