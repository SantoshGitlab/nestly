using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// OTP generation and validation (SRS 11.2.1, 28.1): expiry, single use,
/// retry limits, purpose scoping, and the resend cooldown.
///
/// <see cref="INotificationProvider"/> is mocked throughout — it is the only
/// external dependency, and mocking it both isolates the OTP logic and gives
/// the tests a way to read the plaintext code, which is never persisted.
/// </summary>
public class OtpServiceTests : IDisposable
{
    private const string Mobile = "+919876543210";

    private readonly TestDatabase _database = new();
    private readonly Mock<INotificationProvider> _notificationProvider = new(MockBehavior.Strict);

    /// <summary>
    /// Captures the code out of the outgoing SMS. The service hashes the code
    /// before storing it, so intercepting the message is the only way a test
    /// can know what a legitimate recipient would type in.
    /// </summary>
    private string? _lastSentCode;

    public OtpServiceTests()
    {
        _notificationProvider
            .Setup(p => p.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, message, _) => _lastSentCode = ExtractCode(message))
            .ReturnsAsync(Result.Success());

        _notificationProvider
            .Setup(p => p.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) => _lastSentCode = ExtractCode(body))
            .ReturnsAsync(Result.Success());
    }

    private OtpService CreateService(NestlyDbContext context) =>
        new(context, _notificationProvider.Object);

    private static string ExtractCode(string message) =>
        System.Text.RegularExpressions.Regex.Match(message, @"\d{6}").Value;

    [Fact]
    public async Task GenerateAsync_sends_a_six_digit_code_and_stores_it_hashed()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);

        result.IsSuccess.Should().BeTrue();
        _lastSentCode.Should().MatchRegex(@"^\d{6}$");

        var stored = await context.Set<CustomerOtp>().SingleAsync();
        // The plaintext must never reach the database (CLAUDE.md: never log or
        // persist secrets).
        stored.CodeHash.Should().NotBe(_lastSentCode);
        stored.CodeHash.Should().Be(Sha256Hex(_lastSentCode!));
        stored.ConsumedAt.Should().BeNull();
        stored.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task GenerateAsync_sets_an_expiry_between_three_and_five_minutes_out()
    {
        await using var context = _database.CreateContext();
        var before = DateTime.UtcNow;

        await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Registration);

        var stored = await context.Set<CustomerOtp>().SingleAsync();
        // SRS 11.2.1/28.1 requires a short-lived code; the service uses 5
        // minutes. The assertion allows the 3-5 minute band the requirement
        // states rather than pinning the exact constant.
        stored.ExpiresAt.Should().BeAfter(before.AddMinutes(3));
        stored.ExpiresAt.Should().BeOnOrBefore(before.AddMinutes(5).AddSeconds(1));
    }

    [Fact]
    public async Task ValidateAsync_accepts_the_correct_code_and_consumes_it()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context).ValidateAsync(Mobile, _lastSentCode!, OtpPurpose.Login);
            result.IsSuccess.Should().BeTrue();
        }

        await using (var context = _database.CreateContext())
        {
            var stored = await context.Set<CustomerOtp>().SingleAsync();
            stored.ConsumedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ValidateAsync_rejects_a_code_that_has_already_been_used()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        await using (var context = _database.CreateContext())
        {
            await CreateService(context).ValidateAsync(Mobile, _lastSentCode!, OtpPurpose.Login);
        }

        await using (var context = _database.CreateContext())
        {
            // Replaying a consumed code must fail — single use is what stops a
            // code captured in transit from being reused (SRS 28.3).
            var replay = await CreateService(context).ValidateAsync(Mobile, _lastSentCode!, OtpPurpose.Login);
            replay.IsFailure.Should().BeTrue();
            replay.Error.Code.Should().Be("Otp.NotFound");
        }
    }

    [Fact]
    public async Task ValidateAsync_rejects_an_expired_code()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        // Rewinding the row is how the 3-5 minute auto-expiry gets exercised
        // without the test actually waiting five minutes.
        await using (var context = _database.CreateContext())
        {
            var stored = await context.Set<CustomerOtp>().SingleAsync();
            context.Entry(stored).Property(nameof(CustomerOtp.ExpiresAt)).CurrentValue =
                DateTime.UtcNow.AddSeconds(-1);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context).ValidateAsync(Mobile, _lastSentCode!, OtpPurpose.Login);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Otp.Expired");
        }
    }

    [Fact]
    public async Task ValidateAsync_still_rejects_a_correct_code_once_it_has_expired()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.PasswordReset);
        }

        await using (var context = _database.CreateContext())
        {
            var stored = await context.Set<CustomerOtp>().SingleAsync();
            context.Entry(stored).Property(nameof(CustomerOtp.ExpiresAt)).CurrentValue =
                DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            // The code itself is right; expiry alone must be enough to refuse it.
            var result = await CreateService(context).ValidateAsync(Mobile, _lastSentCode!, OtpPurpose.PasswordReset);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Otp.Expired");
        }
    }

    [Fact]
    public async Task ValidateAsync_counts_each_wrong_guess()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context).ValidateAsync(Mobile, "000000", OtpPurpose.Login);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Otp.Incorrect");
        }

        await using (var context = _database.CreateContext())
        {
            var stored = await context.Set<CustomerOtp>().SingleAsync();
            stored.AttemptCount.Should().Be(1);
            // A wrong guess must not burn the code — the real owner still
            // needs to be able to use it.
            stored.ConsumedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task ValidateAsync_refuses_further_attempts_after_the_retry_limit()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        // Five wrong guesses is the configured ceiling.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await using var context = _database.CreateContext();
            await CreateService(context).ValidateAsync(Mobile, "000000", OtpPurpose.Login);
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context).ValidateAsync(Mobile, "000000", OtpPurpose.Login);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Otp.RetryLimitExceeded");
        }
    }

    [Fact]
    public async Task ValidateAsync_locks_out_the_real_code_once_the_retry_limit_is_hit()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            await using var context = _database.CreateContext();
            await CreateService(context).ValidateAsync(Mobile, "000000", OtpPurpose.Login);
        }

        await using (var context = _database.CreateContext())
        {
            // This is the brute-force guarantee: exhausting the attempts must
            // invalidate the challenge outright, not merely reject bad codes.
            var result = await CreateService(context).ValidateAsync(Mobile, _lastSentCode!, OtpPurpose.Login);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Otp.RetryLimitExceeded");
        }
    }

    [Fact]
    public async Task ValidateAsync_will_not_accept_a_code_issued_for_a_different_purpose()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        await using (var context = _database.CreateContext())
        {
            // Replaying a login code as a password reset would be an account
            // takeover (SRS 28.3), so purpose is part of the lookup.
            var result = await CreateService(context).ValidateAsync(Mobile, _lastSentCode!, OtpPurpose.PasswordReset);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Otp.NotFound");
        }
    }

    [Fact]
    public async Task ValidateAsync_will_not_accept_a_code_issued_to_a_different_target()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context).ValidateAsync("+919000000000", _lastSentCode!, OtpPurpose.Login);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Otp.NotFound");
        }
    }

    [Fact]
    public async Task GenerateAsync_refuses_a_second_request_inside_the_resend_cooldown()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        await using (var context = _database.CreateContext())
        {
            // Without a cooldown, an attacker could reset AttemptCount at will
            // by re-requesting, defeating the retry limit above.
            var result = await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Otp.TooManyRequests");
        }
    }

    [Fact]
    public async Task GenerateAsync_allows_a_retry_once_the_cooldown_has_passed()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        await using (var context = _database.CreateContext())
        {
            var stored = await context.Set<CustomerOtp>().SingleAsync();
            context.Entry(stored).Property(nameof(CustomerOtp.CreatedAt)).CurrentValue =
                DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
            result.IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ValidateAsync_uses_the_newest_code_when_more_than_one_is_pending()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        string firstCode = _lastSentCode!;

        await using (var context = _database.CreateContext())
        {
            var stored = await context.Set<CustomerOtp>().SingleAsync();
            context.Entry(stored).Property(nameof(CustomerOtp.CreatedAt)).CurrentValue =
                DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
        }

        string secondCode = _lastSentCode!;
        secondCode.Should().NotBe(firstCode);

        await using (var context = _database.CreateContext())
        {
            // Re-requesting a code should supersede the previous one, or a
            // customer who asked for a fresh code would be stuck with a stale
            // one they never saw.
            var result = await CreateService(context).ValidateAsync(Mobile, secondCode, OtpPurpose.Login);
            result.IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public async Task GenerateAsync_sends_over_email_when_the_email_channel_is_requested()
    {
        const string email = "customer@example.com";

        await using var context = _database.CreateContext();
        var result = await CreateService(context)
            .GenerateAsync(email, OtpPurpose.EmailChange, NotificationChannel.Email);

        result.IsSuccess.Should().BeTrue();
        _notificationProvider.Verify(
            p => p.SendEmailAsync(email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationProvider.Verify(
            p => p.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_rejects_an_empty_target()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).GenerateAsync("  ", OtpPurpose.Login);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Otp.InvalidTarget");
        // Nothing should have been sent or stored for an invalid request.
        _notificationProvider.VerifyNoOtherCalls();
        (await context.Set<CustomerOtp>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ValidateAsync_reports_no_pending_code_when_none_was_ever_issued()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).ValidateAsync(Mobile, "123456", OtpPurpose.Login);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Otp.NotFound");
    }

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public void Dispose() => _database.Dispose();
}
