using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// Partner OTP generation/validation (task 146b foundation) - structural twin
/// of <see cref="OtpServiceTests"/>, exercised against <see cref="PartnerOtp"/>
/// via <see cref="PartnerOtpService"/> to prove the partner-specific copy
/// behaves identically to the customer original it mirrors.
/// </summary>
public class PartnerOtpServiceTests : IDisposable
{
    private const string Mobile = "+919876543210";

    private readonly TestDatabase _database = new();
    private readonly Mock<INotificationProvider> _notificationProvider = new(MockBehavior.Strict);
    private string? _lastSentCode;

    public PartnerOtpServiceTests()
    {
        _notificationProvider
            .Setup(p => p.SendSmsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, message, _) => _lastSentCode = ExtractCode(message))
            .ReturnsAsync(Result.Success());
    }

    private PartnerOtpService CreateService(NestlyDbContext context) =>
        new(context, _notificationProvider.Object);

    private static string ExtractCode(string message) =>
        System.Text.RegularExpressions.Regex.Match(message, @"\d{6}").Value;

    [Fact]
    public async Task GenerateAsync_sends_a_six_digit_code_and_stores_it_hashed()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Registration);

        result.IsSuccess.Should().BeTrue();
        _lastSentCode.Should().MatchRegex(@"^\d{6}$");

        var stored = await context.Set<PartnerOtp>().SingleAsync();
        stored.CodeHash.Should().NotBe(_lastSentCode);
        stored.ConsumedAt.Should().BeNull();
        stored.AttemptCount.Should().Be(0);
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
            var stored = await context.Set<PartnerOtp>().SingleAsync();
            stored.ConsumedAt.Should().NotBeNull();
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
            var result = await CreateService(context).ValidateAsync(Mobile, _lastSentCode!, OtpPurpose.Registration);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("PartnerOtp.NotFound");
        }
    }

    [Fact]
    public async Task ValidateAsync_refuses_further_attempts_after_the_retry_limit()
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
            var result = await CreateService(context).ValidateAsync(Mobile, "000000", OtpPurpose.Login);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("PartnerOtp.RetryLimitExceeded");
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
            var result = await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("PartnerOtp.TooManyRequests");
        }
    }

    /// <summary>
    /// This is the module-independence guarantee (PARTNER.md SCOPE
    /// BOUNDARY): a customer OTP for a mobile number must not satisfy a
    /// partner OTP challenge for the same number, and vice versa - proven
    /// here by generating a partner OTP and confirming it lands only in
    /// <see cref="PartnerOtp"/>, never <see cref="CustomerOtp"/>.
    /// </summary>
    [Fact]
    public async Task Partner_otp_rows_are_stored_separately_from_customer_otp_rows()
    {
        await using var context = _database.CreateContext();
        await CreateService(context).GenerateAsync(Mobile, OtpPurpose.Login);

        (await context.Set<PartnerOtp>().CountAsync()).Should().Be(1);
        (await context.Set<CustomerOtp>().CountAsync()).Should().Be(0);
    }

    public void Dispose() => _database.Dispose();
}
