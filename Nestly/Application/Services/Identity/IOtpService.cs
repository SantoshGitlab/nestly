using Nestly.Domain.ValueObjects;

namespace Nestly.Application.Services.Identity
{
    public interface IOtpService
    {
        string GenerateOtp();
        bool VerifyOtp(string code);
    }
}
