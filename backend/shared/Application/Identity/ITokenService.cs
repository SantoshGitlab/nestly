namespace Nestly.Application.Identity;

public record AccessToken(string Value, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken GenerateAccessToken(Guid customerId, string mobile);

    /// <summary>A random opaque token; the caller is responsible for hashing it before storage.</summary>
    string GenerateRefreshToken();

    TimeSpan RefreshTokenLifetime { get; }
}
