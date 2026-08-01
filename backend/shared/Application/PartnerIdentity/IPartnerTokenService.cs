namespace Nestly.Application.PartnerIdentity;

public record PartnerAccessToken(string Value, DateTime ExpiresAtUtc);

/// <summary>
/// JWT access/refresh tokens for partners. Deliberately separate from
/// <c>ITokenService</c> (customer tokens) and <c>IAdminTokenService</c> - own
/// signing key, issuer and audience, so a compromised customer or admin key
/// can never be replayed against the partner API and vice versa. Same
/// reasoning as <c>AdminTokenService</c>'s doc comment.
/// </summary>
public interface IPartnerTokenService
{
    PartnerAccessToken GenerateAccessToken(Guid partnerId, string mobile);

    /// <summary>A random opaque token; the caller is responsible for hashing it before storage.</summary>
    string GenerateRefreshToken();

    TimeSpan RefreshTokenLifetime { get; }
}
