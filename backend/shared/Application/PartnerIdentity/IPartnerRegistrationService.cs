using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.PartnerIdentity;

public interface IPartnerRegistrationService
{
    Task<Result> RequestOtpAsync(RequestPartnerRegistrationOtpRequest request);

    Task<Result<PartnerSummaryResponse>> RegisterAsync(RegisterPartnerRequest request);
}
