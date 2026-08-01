using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.PartnerIdentity;

/// <summary>
/// Login, session issuance, refresh, and logout for partners. OTP-only -
/// unlike <c>ICustomerLoginService</c> there is no password login, matching
/// PARTNER.md's API surface.
/// </summary>
public interface IPartnerLoginService
{
    Task<Result> RequestOtpAsync(RequestPartnerLoginOtpRequest request);

    Task<Result<PartnerLoginResponse>> LoginWithOtpAsync(LoginPartnerWithOtpRequest request);

    Task<Result<PartnerLoginResponse>> RefreshAsync(RefreshPartnerTokenRequest request);

    Task<Result> LogoutAsync(LogoutPartnerRequest request);
}
