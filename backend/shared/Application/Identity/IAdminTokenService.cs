namespace Nestly.Application.Identity;

public record AdminAccessToken(string Value, DateTime ExpiresAtUtc);

public interface IAdminTokenService
{
    AdminAccessToken GenerateAccessToken(Guid adminUserId, string email);
}
