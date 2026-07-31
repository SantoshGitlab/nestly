using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "AdminJwt" configuration section (SRS 12.1,
/// 12.1.2, tasks 95a/95e). Deliberately separate from <see cref="JwtOptions"/>
/// (customer tokens): a distinct signing key and audience means a customer
/// token can never be replayed against the admin API, even if one signing
/// key were ever compromised, and the admin session lifetime can be tuned
/// independently of the customer one.
/// </summary>
public class AdminJwtOptions
{
    public const string SectionName = "AdminJwt";

    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "Nestly";

    [Required]
    public string Audience { get; set; } = "Nestly.AdminUsers";

    /// <summary>
    /// Admin panel session timeout (SRS 12.1.2, task 95e). Deliberately
    /// shorter than the customer access token (<see cref="JwtOptions.AccessTokenMinutes"/>,
    /// 15-minute default): an admin token carries back-office privileges, so
    /// a smaller window for a leaked token to be replayed in is worth the
    /// extra re-logins. There is no admin refresh token — re-authentication
    /// once this expires is the timeout mechanism itself.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 10;
}
