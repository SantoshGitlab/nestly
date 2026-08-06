using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "Otp" configuration section. <see cref="Pepper"/>
/// is a server-side secret mixed into every OTP hash via HMAC-SHA256 so a
/// stolen <c>CodeHash</c> row cannot be reversed with a precomputed table over
/// all 1,000,000 six-digit codes (NESTLY-005) - must come from configuration/
/// user-secrets/environment, never a literal in source, same rule as
/// <see cref="JwtOptions.SigningKey"/>.
/// </summary>
public class OtpOptions
{
    public const string SectionName = "Otp";

    [Required, MinLength(32)]
    public string Pepper { get; set; } = string.Empty;
}
