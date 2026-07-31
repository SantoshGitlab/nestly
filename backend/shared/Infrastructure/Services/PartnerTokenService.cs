using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nestly.Application.PartnerIdentity;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// JWT access tokens and opaque refresh tokens for partners. Same shape as
/// <see cref="TokenService"/> (customer tokens), but signed with
/// <see cref="PartnerJwtOptions"/> - see that type's doc comment for why this
/// is a separate signing key rather than a shared one.
/// </summary>
public class PartnerTokenService : IPartnerTokenService
{
    private readonly PartnerJwtOptions _options;

    public PartnerTokenService(IOptions<PartnerJwtOptions> options)
    {
        _options = options.Value;
    }

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public PartnerAccessToken GenerateAccessToken(Guid partnerId, string mobile)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, partnerId.ToString()),
            new Claim("mobile", mobile),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Convert.FromBase64String(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        return new PartnerAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
