using Nestly.Domain.ValueObjects;

namespace Nestly.Application.Services.Identity
{
    public class OtpService : IOtpService
    {
        private const int OtpLength = 6;
        private const string AllowedChars = "0123456789";

        public string GenerateOtp()
        {
            var random = new Random();
            return new string(Enumerable.Repeat(AllowedChars, OtpLength)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public bool VerifyOtp(string code)
        {
            // For demonstration purposes, we'll just check if the code is not empty
            return !string.IsNullOrEmpty(code);
        }
    }
}
