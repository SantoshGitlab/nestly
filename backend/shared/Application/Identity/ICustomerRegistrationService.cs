using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Identity;

public interface ICustomerRegistrationService
{
    Task<Result> RequestOtpAsync(RequestRegistrationOtpRequest request);

    Task<Result<CustomerSummaryResponse>> RegisterAsync(RegisterCustomerRequest request);
}
