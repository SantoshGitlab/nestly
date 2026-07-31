using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "PartnerJwt" configuration section. Own
/// signing key/issuer/audience, same reasoning as <see cref="AdminJwtOptions"/>:
/// a customer or admin token must never be replayable against the partner
/// API. No <c>ValidateOnStart</c> on its registration in
/// <c>DependencyInjection.AddInfrastructure</c> - neither admin-api nor
/// consumer-api define a "PartnerJwt" section, only the future partner-api
/// (task 149) will, so eager validation would fail their startup for a
/// section they have no reason to configure.
/// </summary>
public class PartnerJwtOptions
{
    public const string SectionName = "PartnerJwt";

    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "Nestly";

    [Required]
    public string Audience { get; set; } = "Nestly.Partners";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;
}
