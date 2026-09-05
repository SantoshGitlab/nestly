using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Profile;

/// <summary>
/// Customer profile management (SRS 11.2.3). Every method takes the caller's
/// own customer id, resolved from the JWT by the controller — never from a
/// route or body parameter (SRS 28.3 IDOR).
/// </summary>
public interface ICustomerProfileService
{
    Task<Result<CustomerProfileResponse>> GetAsync(Guid customerId);

    Task<Result<CustomerProfileResponse>> UpdateAsync(Guid customerId, UpdateProfileRequest request);

    Task<Result> RequestMobileChangeOtpAsync(Guid customerId, RequestMobileChangeOtpRequest request);

    Task<Result<CustomerProfileResponse>> ConfirmMobileChangeAsync(Guid customerId, ConfirmMobileChangeRequest request);

    Task<Result> RequestEmailChangeOtpAsync(Guid customerId, RequestEmailChangeOtpRequest request);

    Task<Result<CustomerProfileResponse>> ConfirmEmailChangeAsync(Guid customerId, ConfirmEmailChangeRequest request);

    Task<Result<CommunicationPreferencesResponse>> GetPreferencesAsync(Guid customerId);

    Task<Result<CommunicationPreferencesResponse>> UpdatePreferencesAsync(Guid customerId, CommunicationPreferencesRequest request);

    /// <summary>
    /// Self-service right-to-erasure account deletion. Terminal and
    /// irreversible - unlike every other method here, there is no way back
    /// from this one.
    /// </summary>
    Task<Result> DeleteAccountAsync(Guid customerId);
}
