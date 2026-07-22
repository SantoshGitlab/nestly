using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace backend.shared.Domain
{
    public class OTPService : IOTPService
    {
        private readonly Dictionary<string, OTP> _otpCache = new();
        private const int MaxRetries = 3;
        private const int ExpiryTimeInMinutes = 5;

        public async Task<ValidationResult> GenerateAsync(string phoneNumber)
        {
            if (_otpCache.ContainsKey(phoneNumber))
            {
                var otpInfo = _otpCache[phoneNumber];
                if (DateTime.UtcNow < otpInfo.ExpiryTime && otpInfo.RetryCount < MaxRetries)
                {
                    return ValidationResult.WithErrors(new List<Error>
                    {
                        Error.Validation("OTP_LIMIT", "Too many OTP requests. Please try again later.")
                    });
                }
            }

            var otp = GenerateRandomOTP();
            var expiryTime = DateTime.UtcNow.AddMinutes(ExpiryTimeInMinutes);
            _otpCache[phoneNumber] = new OTP(otp, expiryTime, 0);

            // Simulate sending OTP (e.g., via SMS)
            await SendOTP(phoneNumber, otp);

            return ValidationResult.Success();
        }

        public async Task<ValidationResult> ValidateAsync(string phoneNumber, string providedOTP)
        {
            if (!_otpCache.ContainsKey(phoneNumber))
            {
                return ValidationResult.WithErrors(new List<Error>
                {
                    Error.Validation("INVALID_OTP", "Invalid OTP.")
                });
            }

            var otpInfo = _otpCache[phoneNumber];
            if (DateTime.UtcNow > otpInfo.ExpiryTime)
            {
                _otpCache.Remove(phoneNumber);
                return ValidationResult.WithErrors(new List<Error>
                {
                    Error.Validation("OTP_EXPIRED", "OTP has expired. Please request a new one.")
                });
            }

            if (otpInfo.Otp != providedOTP)
            {
                otpInfo.RetryCount++;
                if (otpInfo.RetryCount >= MaxRetries)
                {
                    _otpCache.Remove(phoneNumber);
                    return ValidationResult.WithErrors(new List<Error>
                    {
                        Error.Validation("OTP_LIMIT", "Too many OTP requests. Please try again later.")
                    });
                }
                return ValidationResult.WithErrors(new List<Error>
                {
                    Error.Validation("INVALID_OTP", "Invalid OTP.")
                });
            }

            _otpCache.Remove(phoneNumber);
            return ValidationResult.Success();
        }

        private string GenerateRandomOTP()
        {
            // Generate a random 6-digit OTP
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private async Task SendOTP(string phoneNumber, string otp)
        {
            // Simulate sending OTP (e.g., via SMS)
            Console.WriteLine($"Sending OTP {otp} to {phoneNumber}");
        }
    }

    public interface IOTPService
    {
        Task<ValidationResult> GenerateAsync(string phoneNumber);
        Task<ValidationResult> ValidateAsync(string phoneNumber, string providedOTP);
    }

    internal class OTP
    {
        public string Otp { get; }
        public DateTime ExpiryTime { get; }
        public int RetryCount { get; private set; }

        public OTP(string otp, DateTime expiryTime, int retryCount)
        {
            Otp = otp;
            ExpiryTime = expiryTime;
            RetryCount = retryCount;
        }
    }
}
