using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Identity;

public interface ICustomerRegistrationService
{
    Task<Result> RequestOtpAsync(RequestRegistrationOtpRequest request);

    Task<Result<CustomerSummaryResponse>> RegisterAsync(RegisterCustomerRequest request);

    /// <summary>Email-first counterpart of <see cref="RequestOtpAsync"/>: sends the OTP to an email address instead of a mobile number.</summary>
    Task<Result> RequestEmailOtpAsync(RequestRegistrationEmailOtpRequest request);

    /// <summary>Email-first counterpart of <see cref="RegisterAsync"/>: the OTP proves email ownership instead of mobile ownership.</summary>
    Task<Result<CustomerSummaryResponse>> RegisterWithEmailAsync(RegisterCustomerWithEmailRequest request);
}
