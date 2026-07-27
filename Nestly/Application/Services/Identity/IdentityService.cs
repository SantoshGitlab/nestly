using Nestly.Application.Services.Identity;
using Nestly.Domain.ValueObjects;

namespace Nestly.Application.Services.Identity
{
    public class IdentityService : IIdentityService
    {
        private readonly IOtpService _otpService;
        private readonly IUserRepository _userRepository;

        public IdentityService(IOtpService otpService, IUserRepository userRepository)
        {
            _otpService = otpService;
            _userRepository = userRepository;
        }

        public async Task<Result> SendForgotPasswordVerificationCodeAsync(string email, string mobile)
        {
            var user = await _userRepository.GetByEmailOrMobileAsync(email, mobile);
            if (user == null)
                return Result.Failure(Error.Unauthorized("User not found"));

            var code = _otpService.GenerateOtp();
            // Here you would typically send the OTP via SMS or email
            // For demonstration purposes, we'll just log it
            Console.WriteLine($"OTP sent to {email}/{mobile}: {code}");

            user.SetForgotPasswordCode(code);
            await _userRepository.UpdateAsync(user);

            return Result.Success();
        }

        public async Task<Result> VerifyForgotPasswordCodeAsync(string email, string mobile, string code)
        {
            var user = await _userRepository.GetByEmailOrMobileAsync(email, mobile);
            if (user == null || !user.VerifyForgotPasswordCode(code))
                return Result.Failure(Error.Unauthorized("Invalid OTP"));

            // OTP is valid, proceed with password reset
            return Result.Success();
        }

        public async Task<Result> ResetForgotPasswordAsync(string email, string mobile, string newPassword)
        {
            var user = await _userRepository.GetByEmailOrMobileAsync(email, mobile);
            if (user == null || !user.VerifyForgotPasswordCode(newPassword))
                return Result.Failure(Error.Unauthorized("Invalid password"));

            user.SetPasswordHash(newPassword);
            await _userRepository.UpdateAsync(user);

            return Result.Success();
        }
    }
}
