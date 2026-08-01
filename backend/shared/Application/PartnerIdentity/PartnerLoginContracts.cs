namespace Nestly.Application.PartnerIdentity;

public record RequestPartnerLoginOtpRequest(string Mobile);

public record LoginPartnerWithOtpRequest(string Mobile, string OtpCode);

public record RefreshPartnerTokenRequest(string RefreshToken);

public record LogoutPartnerRequest(string RefreshToken);

public record PartnerLoginResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken);
