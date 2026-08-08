using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.ProviderIdentity;

/// <summary>
/// Login, session issuance, refresh, and logout for providers. OTP-only -
/// unlike <c>ICustomerLoginService</c> there is no password login, matching
/// PROVIDER.md's API surface.
/// </summary>
public interface IProviderLoginService
{
    Task<Result> RequestOtpAsync(RequestProviderLoginOtpRequest request);

    Task<Result<ProviderLoginResponse>> LoginWithOtpAsync(LoginProviderWithOtpRequest request);

    Task<Result<ProviderLoginResponse>> RefreshAsync(RefreshProviderTokenRequest request);

    Task<Result> LogoutAsync(LogoutProviderRequest request);

    /// <summary>
    /// Dev-only: issues a real session for a seeded provider without OTP
    /// verification. Reuses the same session-issuing path as
    /// <see cref="LoginWithOtpAsync"/> — see provider-api's Program.cs for the
    /// environment/secret gating that makes this unreachable outside
    /// Development. Never call this from a production code path.
    /// </summary>
    Task<Result<ProviderLoginResponse>> DevLoginAsync(string mobile);
}
