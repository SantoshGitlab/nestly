namespace Nestly.Application.ProviderIdentity;

public record RequestProviderLoginOtpRequest(string Mobile);

public record LoginProviderWithOtpRequest(string Mobile, string OtpCode);

/// <summary>Task 372: email+password login, mirroring <c>LoginWithPasswordRequest</c>.</summary>
public record LoginProviderWithPasswordRequest(string Email, string Password);

public record RefreshProviderTokenRequest(string RefreshToken);

public record LogoutProviderRequest(string RefreshToken);

public record ProviderLoginResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken);
