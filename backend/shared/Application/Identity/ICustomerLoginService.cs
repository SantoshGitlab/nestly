using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Identity;

public interface ICustomerLoginService
{
    Task<Result> RequestOtpAsync(RequestLoginOtpRequest request);

    Task<Result<LoginResponse>> LoginWithOtpAsync(LoginWithOtpRequest request);

    Task<Result<LoginResponse>> LoginWithPasswordAsync(LoginWithPasswordRequest request);

    Task<Result<LoginResponse>> RefreshAsync(RefreshTokenRequest request);

    Task<Result> LogoutAsync(LogoutRequest request);
}
