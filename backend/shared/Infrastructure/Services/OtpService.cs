using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// OTP generation/validation (SRS 11.2.1, 28.1): expiring, hashed, single-use
/// codes with an attempt limit and a cooldown against rapid re-requests.
/// </summary>
public class OtpService : IOTPService
{
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);
    private const int MaxAttempts = 5;

    private readonly NestlyDbContext _context;

    public OtpService(NestlyDbContext context)
    {
        _context = context;
    }

    public async Task<Result> GenerateAsync(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return Result.Failure(Error.Validation("Otp.InvalidTarget", "Phone number is required."));
        }

        var now = DateTime.UtcNow;

        bool requestedRecently = await _context.Set<CustomerOtp>()
            .AnyAsync(o => o.Target == phoneNumber && o.CreatedAt > now.Subtract(ResendCooldown));
        if (requestedRecently)
        {
            return Result.Failure(Error.Business("Otp.TooManyRequests",
                "Please wait before requesting another OTP."));
        }

        string code = GenerateNumericCode(6);
        var otp = new CustomerOtp(Guid.NewGuid(), customerId: null, phoneNumber, OtpPurpose.Login,
            Hash(code), now.Add(Expiry));

        await _context.Set<CustomerOtp>().AddAsync(otp);
        await _context.SaveChangesAsync();

        // In production this hands off to an SMS provider (task 26 area); for
        // now the plaintext code only ever exists in memory here and is never
        // persisted or logged, matching the no-PII/no-secrets logging rule.
        return Result.Success();
    }

    public async Task<Result> ValidateAsync(string phoneNumber, string otpCode)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(otpCode))
        {
            return Result.Failure(Error.Validation("Otp.InvalidInput", "Phone number and OTP code are required."));
        }

        var otp = await _context.Set<CustomerOtp>()
            .Where(o => o.Target == phoneNumber && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp is null)
        {
            return Result.Failure(Error.NotFound("Otp.NotFound", "No pending OTP for this number."));
        }

        if (otp.AttemptCount >= MaxAttempts)
        {
            return Result.Failure(Error.Business("Otp.RetryLimitExceeded", "Too many incorrect attempts."));
        }

        if (otp.IsExpired(DateTime.UtcNow))
        {
            return Result.Failure(Error.Business("Otp.Expired", "This OTP has expired."));
        }

        otp.RecordAttempt();

        if (otp.CodeHash != Hash(otpCode))
        {
            await _context.SaveChangesAsync();
            return Result.Failure(Error.Validation("Otp.Incorrect", "The OTP code is incorrect."));
        }

        otp.MarkConsumed();
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    private static string GenerateNumericCode(int digits)
    {
        var builder = new StringBuilder(digits);
        for (int i = 0; i < digits; i++)
        {
            builder.Append(RandomNumberGenerator.GetInt32(0, 10));
        }
        return builder.ToString();
    }

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
