using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.ProviderIdentity;

/// <summary>Forgot/reset password for providers (task 372), mirroring <c>ICustomerPasswordResetService</c>.</summary>
public interface IProviderPasswordResetService
{
    Task<Result> RequestResetAsync(ForgotProviderPasswordRequest request);

    Task<Result> ResetAsync(ResetProviderPasswordRequest request);
}
