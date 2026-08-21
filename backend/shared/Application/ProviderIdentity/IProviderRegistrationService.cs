using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.ProviderIdentity;

public interface IProviderRegistrationService
{
    Task<Result> RequestOtpAsync(RequestProviderRegistrationOtpRequest request);

    Task<Result<ProviderSummaryResponse>> RegisterAsync(RegisterProviderRequest request);

    /// <summary>Email-first counterpart of <see cref="RequestOtpAsync"/>: sends the OTP to an email address instead of a mobile number.</summary>
    Task<Result> RequestEmailOtpAsync(RequestProviderRegistrationEmailOtpRequest request);

    /// <summary>Email-first counterpart of <see cref="RegisterAsync"/>: the OTP proves email ownership instead of mobile ownership.</summary>
    Task<Result<ProviderSummaryResponse>> RegisterWithEmailAsync(RegisterProviderWithEmailRequest request);
}
