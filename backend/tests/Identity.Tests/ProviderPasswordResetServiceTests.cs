using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nestly.Application;
using Nestly.Application.ProviderIdentity;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// Forgot/reset password for providers (task 372), structurally mirroring
/// <see cref="PasswordResetServiceTests"/> in full: who may request a code,
/// what a code is allowed to do, and what happens to existing sessions once
/// the password changes.
/// </summary>
public class ProviderPasswordResetServiceTests : IDisposable
{
    private const string Email = "ravi@example.com";
    private const string Mobile = "+919876543210";
    private const string OldPassword = "old-password-value";
    private const string NewPassword = "new-password-value";
    private const string ValidCode = "123456";

    private readonly TestDatabase _database = new();
    private readonly Mock<IProviderOtpService> _otpService = new();
    private Guid _providerId;

    public ProviderPasswordResetServiceTests()
    {
        _otpService
            .Setup(o => o.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OtpPurpose>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("Otp.NotFound", "No pending OTP for this request.")));
        _otpService
            .Setup(o => o.ValidateAsync(Mobile, ValidCode, OtpPurpose.PasswordReset))
            .ReturnsAsync(Result.Success());
        _otpService
            .Setup(o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<NotificationChannel>()))
            .ReturnsAsync(Result.Success());

        SeedProvider();
    }

    private void SeedProvider()
    {
        using var context = _database.CreateContext();
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, Mobile, Email);
        _providerId = provider.Id;
        context.Add(provider);

        var identity = new ProviderAuthIdentity(
            Guid.NewGuid(), provider.Id, AuthProviderType.EmailPassword, Email, isPrimary: false);
        identity.SetPasswordHash(new PasswordHasher<Provider>().HashPassword(provider, OldPassword));
        context.Add(identity);

        context.SaveChanges();
    }

    private ProviderPasswordResetService CreateService(NestlyDbContext context, ProviderAccountOptions? options = null) =>
        new(
            new ProviderRepository(context),
            new ProviderAuthIdentityRepository(context),
            new ProviderSessionRepository(context),
            _otpService.Object,
            Options.Create(options ?? new ProviderAccountOptions()),
            NullLogger<ProviderPasswordResetService>.Instance);

    private void SeedActiveSession(string refreshTokenHash)
    {
        using var context = _database.CreateContext();
        var now = DateTime.UtcNow;
        context.Add(new ProviderSession(Guid.NewGuid(), _providerId, refreshTokenHash, now, now.AddDays(7)));
        context.SaveChanges();
    }

    [Fact]
    public async Task RequestResetAsync_sends_the_code_to_the_mobile_on_file_not_the_email()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).RequestResetAsync(new ForgotProviderPasswordRequest(Email));

        result.IsSuccess.Should().BeTrue();
        // The email is only an identifier here — it has never been proven to
        // belong to the provider, so mailing a reset code to it would be an
        // account-takeover path.
        _otpService.Verify(
            o => o.GenerateAsync(Mobile, OtpPurpose.PasswordReset, NotificationChannel.Sms),
            Times.Once);
        _otpService.Verify(
            o => o.GenerateAsync(Email, It.IsAny<OtpPurpose>(), It.IsAny<NotificationChannel>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestResetAsync_reports_success_for_an_unknown_email_without_sending_anything()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context)
            .RequestResetAsync(new ForgotProviderPasswordRequest("nobody@example.com"));

        // Succeeding either way is deliberate: a 404 would confirm which
        // addresses are registered (mirrors CustomerPasswordResetService).
        result.IsSuccess.Should().BeTrue();
        _otpService.Verify(
            o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<NotificationChannel>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestResetAsync_does_not_send_a_code_for_a_suspended_provider()
    {
        await using (var context = _database.CreateContext())
        {
            var provider = await context.Set<Provider>().SingleAsync(p => p.Id == _providerId);
            provider.ChangeStatus(ProviderStatus.Suspended);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context).RequestResetAsync(new ForgotProviderPasswordRequest(Email));

            result.IsSuccess.Should().BeTrue();
            _otpService.Verify(
                o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<NotificationChannel>()),
                Times.Never);
        }
    }

    [Fact]
    public async Task RequestResetAsync_is_refused_when_password_auth_is_disabled()
    {
        await using var context = _database.CreateContext();
        var options = new ProviderAccountOptions { PasswordAuthEnabled = false };

        var result = await CreateService(context, options).RequestResetAsync(new ForgotProviderPasswordRequest(Email));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProviderPasswordReset.PasswordAuthDisabled");
    }

    [Fact]
    public async Task ResetAsync_replaces_the_password_hash_when_the_code_verifies()
    {
        string? originalHash;
        await using (var context = _database.CreateContext())
        {
            originalHash = (await context.Set<ProviderAuthIdentity>().SingleAsync()).PasswordHash;
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context)
                .ResetAsync(new ResetProviderPasswordRequest(Email, ValidCode, NewPassword));
            result.IsSuccess.Should().BeTrue();
        }

        await using (var context = _database.CreateContext())
        {
            var identity = await context.Set<ProviderAuthIdentity>().SingleAsync();
            var provider = await context.Set<Provider>().SingleAsync();

            identity.PasswordHash.Should().NotBe(originalHash);
            // Stored hashed, never in the clear.
            identity.PasswordHash.Should().NotContain(NewPassword);

            var hasher = new PasswordHasher<Provider>();
            hasher.VerifyHashedPassword(provider, identity.PasswordHash!, NewPassword)
                .Should().NotBe(PasswordVerificationResult.Failed);
            hasher.VerifyHashedPassword(provider, identity.PasswordHash!, OldPassword)
                .Should().Be(PasswordVerificationResult.Failed);
        }
    }

    [Fact]
    public async Task ResetAsync_revokes_every_active_session()
    {
        SeedActiveSession("HASH-ONE");
        SeedActiveSession("HASH-TWO");

        await using (var context = _database.CreateContext())
        {
            await CreateService(context).ResetAsync(new ResetProviderPasswordRequest(Email, ValidCode, NewPassword));
        }

        await using (var context = _database.CreateContext())
        {
            // A refresh token stolen before the reset must stop working the
            // moment the password changes, or the reset achieves nothing.
            var sessions = await context.Set<ProviderSession>().ToListAsync();
            sessions.Should().HaveCount(2);
            sessions.Should().OnlyContain(s => s.RevokedAt != null);
        }
    }

    [Fact]
    public async Task ResetAsync_leaves_another_providers_sessions_alone()
    {
        var otherProviderId = Guid.NewGuid();
        await using (var context = _database.CreateContext())
        {
            var now = DateTime.UtcNow;
            context.Add(new ProviderSession(Guid.NewGuid(), otherProviderId, "OTHER-HASH", now, now.AddDays(7)));
            await context.SaveChangesAsync();
        }

        SeedActiveSession("HASH-ONE");

        await using (var context = _database.CreateContext())
        {
            await CreateService(context).ResetAsync(new ResetProviderPasswordRequest(Email, ValidCode, NewPassword));
        }

        await using (var context = _database.CreateContext())
        {
            var other = await context.Set<ProviderSession>()
                .SingleAsync(s => s.ProviderId == otherProviderId);
            other.RevokedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task ResetAsync_rejects_a_wrong_code_and_leaves_the_password_intact()
    {
        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context)
                .ResetAsync(new ResetProviderPasswordRequest(Email, "000000", NewPassword));
            result.IsFailure.Should().BeTrue();
        }

        await using (var context = _database.CreateContext())
        {
            var identity = await context.Set<ProviderAuthIdentity>().SingleAsync();
            var provider = await context.Set<Provider>().SingleAsync();

            new PasswordHasher<Provider>()
                .VerifyHashedPassword(provider, identity.PasswordHash!, OldPassword)
                .Should().NotBe(PasswordVerificationResult.Failed);
        }
    }

    [Fact]
    public async Task ResetAsync_rejects_an_unknown_email()
    {
        await using var context = _database.CreateContext();
        var service = CreateService(context);

        var unknownEmail = await service.ResetAsync(
            new ResetProviderPasswordRequest("nobody@example.com", ValidCode, NewPassword));

        unknownEmail.IsFailure.Should().BeTrue();
        unknownEmail.Error.Code.Should().Be("ProviderPasswordReset.Invalid");
    }

    public void Dispose() => _database.Dispose();
}
