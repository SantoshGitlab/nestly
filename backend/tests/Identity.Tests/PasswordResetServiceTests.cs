using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nestly.Application;
using Nestly.Application.Identity;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// Forgot/reset password (SRS 11.2.2), covering the whole flow: who may
/// request a code, what a code is allowed to do, and what happens to existing
/// sessions once the password changes.
/// </summary>
public class PasswordResetServiceTests : IDisposable
{
    private const string Email = "customer@example.com";
    private const string Mobile = "+919876543210";
    private const string OldPassword = "old-password-value";
    private const string NewPassword = "new-password-value";
    private const string ValidCode = "123456";

    private readonly TestDatabase _database = new();
    private readonly Mock<IOTPService> _otpService = new();
    private Guid _customerId;

    public PasswordResetServiceTests()
    {
        // Accepts exactly one code against exactly the account's mobile
        // number and purpose; everything else fails. That shape is what lets
        // the tests below assert on *which* target the service verifies.
        _otpService
            .Setup(o => o.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OtpPurpose>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("Otp.NotFound", "No pending OTP for this request.")));
        _otpService
            .Setup(o => o.ValidateAsync(Mobile, ValidCode, OtpPurpose.PasswordReset))
            .ReturnsAsync(Result.Success());
        _otpService
            .Setup(o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<NotificationChannel>()))
            .ReturnsAsync(Result.Success());

        SeedCustomer();
    }

    private void SeedCustomer()
    {
        using var context = _database.CreateContext();
        var customer = new Customer(Guid.NewGuid(), Mobile, "Test Customer", CustomerStatus.Active, Email);
        _customerId = customer.Id;
        context.Add(customer);

        var identity = new CustomerAuthIdentity(
            Guid.NewGuid(), customer.Id, AuthProviderType.EmailPassword, Email, isPrimary: false);
        identity.SetPasswordHash(new PasswordHasher<Customer>().HashPassword(customer, OldPassword));
        context.Add(identity);

        context.SaveChanges();
    }

    private CustomerPasswordResetService CreateService(NestlyDbContext context, AccountOptions? options = null) =>
        new(
            new CustomerRepository(context),
            new CustomerAuthIdentityRepository(context),
            new CustomerSessionRepository(context),
            _otpService.Object,
            Options.Create(options ?? new AccountOptions()),
            NullLogger<CustomerPasswordResetService>.Instance);

    private void SeedActiveSession(string refreshTokenHash)
    {
        using var context = _database.CreateContext();
        var now = DateTime.UtcNow;
        context.Add(new CustomerSession(Guid.NewGuid(), _customerId, refreshTokenHash, now, now.AddDays(7)));
        context.SaveChanges();
    }

    [Fact]
    public async Task RequestResetAsync_sends_the_code_to_the_mobile_on_file_not_the_email()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).RequestResetAsync(new ForgotPasswordRequest(Email));

        result.IsSuccess.Should().BeTrue();
        // The email is only an identifier here — it has never been proven to
        // belong to the customer, so mailing a reset code to it would be an
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
            .RequestResetAsync(new ForgotPasswordRequest("nobody@example.com"));

        // Succeeding either way is deliberate: a 404 would confirm which
        // addresses are registered (SRS 28.3 enumeration).
        result.IsSuccess.Should().BeTrue();
        _otpService.Verify(
            o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<NotificationChannel>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestResetAsync_does_not_send_a_code_for_a_blocked_account()
    {
        await using (var context = _database.CreateContext())
        {
            var customer = await context.Set<Customer>().SingleAsync(c => c.Id == _customerId);
            customer.UpdateStatus(CustomerStatus.Blocked);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context).RequestResetAsync(new ForgotPasswordRequest(Email));

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
        var options = new AccountOptions { PasswordAuthEnabled = false };

        var result = await CreateService(context, options).RequestResetAsync(new ForgotPasswordRequest(Email));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PasswordReset.PasswordAuthDisabled");
    }

    [Fact]
    public async Task ResetAsync_replaces_the_password_hash_when_the_code_verifies()
    {
        string? originalHash;
        await using (var context = _database.CreateContext())
        {
            originalHash = (await context.Set<CustomerAuthIdentity>().SingleAsync()).PasswordHash;
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context)
                .ResetAsync(new ResetPasswordRequest(Email, ValidCode, NewPassword));
            result.IsSuccess.Should().BeTrue();
        }

        await using (var context = _database.CreateContext())
        {
            var identity = await context.Set<CustomerAuthIdentity>().SingleAsync();
            var customer = await context.Set<Customer>().SingleAsync();

            identity.PasswordHash.Should().NotBe(originalHash);
            // Stored hashed, never in the clear.
            identity.PasswordHash.Should().NotContain(NewPassword);

            var hasher = new PasswordHasher<Customer>();
            hasher.VerifyHashedPassword(customer, identity.PasswordHash!, NewPassword)
                .Should().NotBe(PasswordVerificationResult.Failed);
            hasher.VerifyHashedPassword(customer, identity.PasswordHash!, OldPassword)
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
            await CreateService(context).ResetAsync(new ResetPasswordRequest(Email, ValidCode, NewPassword));
        }

        await using (var context = _database.CreateContext())
        {
            // A refresh token stolen before the reset must stop working the
            // moment the password changes, or the reset achieves nothing.
            var sessions = await context.Set<CustomerSession>().ToListAsync();
            sessions.Should().HaveCount(2);
            sessions.Should().OnlyContain(s => s.RevokedAt != null);
        }
    }

    [Fact]
    public async Task ResetAsync_leaves_another_customers_sessions_alone()
    {
        var otherCustomerId = Guid.NewGuid();
        await using (var context = _database.CreateContext())
        {
            var now = DateTime.UtcNow;
            context.Add(new CustomerSession(Guid.NewGuid(), otherCustomerId, "OTHER-HASH", now, now.AddDays(7)));
            await context.SaveChangesAsync();
        }

        SeedActiveSession("HASH-ONE");

        await using (var context = _database.CreateContext())
        {
            await CreateService(context).ResetAsync(new ResetPasswordRequest(Email, ValidCode, NewPassword));
        }

        await using (var context = _database.CreateContext())
        {
            var other = await context.Set<CustomerSession>()
                .SingleAsync(s => s.CustomerId == otherCustomerId);
            other.RevokedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task ResetAsync_rejects_a_wrong_code_and_leaves_the_password_intact()
    {
        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context)
                .ResetAsync(new ResetPasswordRequest(Email, "000000", NewPassword));
            result.IsFailure.Should().BeTrue();
        }

        await using (var context = _database.CreateContext())
        {
            var identity = await context.Set<CustomerAuthIdentity>().SingleAsync();
            var customer = await context.Set<Customer>().SingleAsync();

            new PasswordHasher<Customer>()
                .VerifyHashedPassword(customer, identity.PasswordHash!, OldPassword)
                .Should().NotBe(PasswordVerificationResult.Failed);
        }
    }

    [Fact]
    public async Task ResetAsync_gives_an_unknown_email_the_same_answer_as_a_bad_code()
    {
        await using var context = _database.CreateContext();
        var service = CreateService(context);

        var unknownEmail = await service.ResetAsync(
            new ResetPasswordRequest("nobody@example.com", ValidCode, NewPassword));

        unknownEmail.IsFailure.Should().BeTrue();
        unknownEmail.Error.Code.Should().Be("PasswordReset.Invalid");
    }

    [Fact]
    public async Task ResetAsync_refuses_a_blocked_account()
    {
        await using (var context = _database.CreateContext())
        {
            var customer = await context.Set<Customer>().SingleAsync(c => c.Id == _customerId);
            customer.UpdateStatus(CustomerStatus.Blocked);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context)
                .ResetAsync(new ResetPasswordRequest(Email, ValidCode, NewPassword));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("PasswordReset.Invalid");
        }
    }

    [Fact]
    public async Task ResetAsync_only_accepts_a_code_issued_for_the_password_reset_purpose()
    {
        await using var context = _database.CreateContext();
        await CreateService(context).ResetAsync(new ResetPasswordRequest(Email, ValidCode, NewPassword));

        // A login or registration code must not be usable here — the purpose
        // is part of what the service asks the OTP service to verify.
        _otpService.Verify(
            o => o.ValidateAsync(Mobile, ValidCode, OtpPurpose.PasswordReset),
            Times.Once);
        _otpService.Verify(
            o => o.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), OtpPurpose.Login),
            Times.Never);
    }

    public void Dispose() => _database.Dispose();
}
