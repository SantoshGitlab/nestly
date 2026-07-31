namespace Nestly.Application.Identity;

/// <summary>Admin panel login request (SRS 12.1.1, task 95a).</summary>
public record AdminLoginRequest(string Email, string Password);

/// <summary>
/// Admin panel session. Access-token only — there is no admin refresh token:
/// the short, fixed token lifetime is itself the session-timeout mechanism
/// (task 95e), so re-authentication after expiry is the intended behaviour
/// rather than something a refresh flow needs to smooth over.
/// </summary>
public record AdminLoginResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc);
