namespace Nestly.Application.Identity;

public record RequestLoginOtpRequest(string Mobile);

public record LoginWithOtpRequest(string Mobile, string OtpCode);

public record LoginWithPasswordRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record LoginResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken);
