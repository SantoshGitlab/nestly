using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "Jwt" configuration section (SRS 11.2.2:
/// JWT access+refresh tokens). <see cref="SigningKey"/> must come from
/// configuration/user-secrets/environment, never a literal in source.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "Nestly";

    [Required]
    public string Audience { get; set; } = "Nestly.Customers";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;
}
