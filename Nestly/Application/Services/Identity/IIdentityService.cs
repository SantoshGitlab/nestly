using Nestly.Domain.ValueObjects;

namespace Nestly.Application.Services.Identity
{
    public interface IIdentityService
    {
        Task<Result> SendForgotPasswordVerificationCodeAsync(string email, string mobile);
        Task<Result> VerifyForgotPasswordCodeAsync(string email, string mobile, string code);
        Task<Result> ResetForgotPasswordAsync(string email, string mobile, string newPassword);
    }
}
