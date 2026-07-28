using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Identity;

public interface ICustomerPasswordResetService
{
    /// <summary>
    /// Issues a password-reset OTP. Succeeds whether or not the email maps to
    /// an account, so the response cannot be used to enumerate registered
    /// addresses (SRS 28.3).
    /// </summary>
    Task<Result> RequestResetAsync(ForgotPasswordRequest request);

    /// <summary>
    /// Replaces the password hash once the OTP verifies, and revokes every
    /// existing session so a stolen refresh token cannot outlive the reset.
    /// </summary>
    Task<Result> ResetAsync(ResetPasswordRequest request);
}
