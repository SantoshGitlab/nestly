using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nestly.Application.Identity;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// JWT access tokens for the admin panel (SRS 12.1, tasks 95a/95e). Separate
/// from <see cref="TokenService"/> (customer tokens) rather than a shared
/// parameterised method: admin tokens use their own signing key, issuer,
/// audience and lifetime (<see cref="AdminJwtOptions"/>), so a compromised
/// customer key cannot be replayed against the admin API and vice versa, and
/// the admin session lifetime can be tuned independently.
///
/// No role claims yet: <c>AdminUser</c>-to-<c>AdminRole</c> assignment does
/// not exist until task 97b, so this token only proves identity for now —
/// authorization (task 96b/96c) adds to this once roles can actually be
/// assigned.
/// </summary>
public class AdminTokenService : IAdminTokenService
{
    private readonly AdminJwtOptions _options;

    public AdminTokenService(IOptions<AdminJwtOptions> options)
    {
        _options = options.Value;
    }

    public AdminAccessToken GenerateAccessToken(Guid adminUserId, string email)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, adminUserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
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

        return new AdminAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
