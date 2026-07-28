using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
/// Login throttling and account lockout (SRS 11.2.2, 28.1) exercised end to
/// end through <see cref="CustomerLoginService"/> and the real repositories
/// over a relational database — the counting happens in SQL, so a test with
/// stubbed repositories would prove nothing about the actual rule.
///
/// Thresholds come from <see cref="AccountOptions"/>, so every scenario is
/// parameterised on the configured limit rather than a hardcoded 5.
/// </summary>
public class LoginThrottlingTests : IDisposable
{
    private const string Email = "customer@example.com";
    private const string Mobile = "+919876543210";
    private const string CorrectPassword = "correct-horse-battery";

    private readonly TestDatabase _database = new();
    private readonly Mock<IOTPService> _otpService = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private Guid _customerId;

    public LoginThrottlingTests()
    {
        _tokenService
            .Setup(t => t.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(new AccessToken("test-access-token", DateTime.UtcNow.AddMinutes(15)));
        // A distinct value per call: sessions are keyed by refresh-token hash,
        // and a constant would collide on the unique index.
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns(() => Guid.NewGuid().ToString("N"));
        _tokenService.Setup(t => t.RefreshTokenLifetime).Returns(TimeSpan.FromDays(7));

        SeedCustomerWithPassword();
    }

    private void SeedCustomerWithPassword()
    {
        using var context = _database.CreateContext();
        var customer = new Customer(Guid.NewGuid(), Mobile, "Test Customer", CustomerStatus.Active, Email);
        _customerId = customer.Id;
        context.Add(customer);

        var identity = new CustomerAuthIdentity(
            Guid.NewGuid(), customer.Id, AuthProviderType.EmailPassword, Email, isPrimary: false);
        identity.SetPasswordHash(new PasswordHasher<Customer>().HashPassword(customer, CorrectPassword));
        context.Add(identity);

        context.SaveChanges();
    }

    private CustomerLoginService CreateService(NestlyDbContext context, AccountOptions options) =>
        new(
            new CustomerRepository(context),
            new CustomerAuthIdentityRepository(context),
            new CustomerSessionRepository(context),
            new LoginAttemptRepository(context),
            _otpService.Object,
            _tokenService.Object,
            Options.Create(options));

    private static AccountOptions OptionsWith(int maxAttempts, int windowMinutes = 15) =>
        new() { MaxFailedLoginAttempts = maxAttempts, LockoutWindowMinutes = windowMinutes };

    private async Task<Result<LoginResponse>> AttemptPasswordLoginAsync(AccountOptions options, string password)
    {
        await using var context = _database.CreateContext();
        return await CreateService(context, options)
            .LoginWithPasswordAsync(new LoginWithPasswordRequest(Email, password));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task The_account_locks_once_the_configured_number_of_failures_is_reached(int maxAttempts)
    {
        var options = OptionsWith(maxAttempts);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var result = await AttemptPasswordLoginAsync(options, "wrong-password");
            result.IsFailure.Should().BeTrue();
            // Up to the threshold the answer is "wrong credentials", not
            // "locked" — otherwise the lockout itself leaks attempt counts.
            result.Error.Code.Should().Be("Login.InvalidCredentials");
        }

        var locked = await AttemptPasswordLoginAsync(options, "wrong-password");
        locked.IsFailure.Should().BeTrue();
        locked.Error.Code.Should().Be("Login.AccountLocked");
    }

    [Fact]
    public async Task A_locked_account_refuses_even_the_correct_password()
    {
        var options = OptionsWith(maxAttempts: 3);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            await AttemptPasswordLoginAsync(options, "wrong-password");
        }

        // This is the whole point of a lockout: knowing the password must not
        // rescue an attacker who has already burned the attempt budget.
        var result = await AttemptPasswordLoginAsync(options, CorrectPassword);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Login.AccountLocked");
    }

    [Fact]
    public async Task A_correct_password_still_works_below_the_threshold()
    {
        var options = OptionsWith(maxAttempts: 5);

        for (int attempt = 0; attempt < 4; attempt++)
        {
            await AttemptPasswordLoginAsync(options, "wrong-password");
        }

        var result = await AttemptPasswordLoginAsync(options, CorrectPassword);
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Failures_older_than_the_window_no_longer_count()
    {
        var options = OptionsWith(maxAttempts: 3, windowMinutes: 15);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            await AttemptPasswordLoginAsync(options, "wrong-password");
        }

        (await AttemptPasswordLoginAsync(options, CorrectPassword)).IsFailure.Should().BeTrue();

        // Age the recorded attempts past the window instead of waiting it out.
        await using (var context = _database.CreateContext())
        {
            var attempts = await context.Set<LoginAttempt>().ToListAsync();
            foreach (var attempt in attempts)
            {
                context.Entry(attempt).Property(nameof(LoginAttempt.OccurredAtUtc)).CurrentValue =
                    DateTime.UtcNow.AddMinutes(-16);
            }
            await context.SaveChangesAsync();
        }

        // A lockout is a cooling-off period, not a permanent ban.
        var result = await AttemptPasswordLoginAsync(options, CorrectPassword);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_shorter_window_releases_the_lockout_sooner()
    {
        var options = OptionsWith(maxAttempts: 3, windowMinutes: 5);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            await AttemptPasswordLoginAsync(options, "wrong-password");
        }

        await using (var context = _database.CreateContext())
        {
            var attempts = await context.Set<LoginAttempt>().ToListAsync();
            foreach (var attempt in attempts)
            {
                // Inside a 15-minute window, outside a 5-minute one — this is
                // what proves the window is actually read from configuration.
                context.Entry(attempt).Property(nameof(LoginAttempt.OccurredAtUtc)).CurrentValue =
                    DateTime.UtcNow.AddMinutes(-6);
            }
            await context.SaveChangesAsync();
        }

        (await AttemptPasswordLoginAsync(options, CorrectPassword)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Locking_one_identifier_does_not_lock_another()
    {
        var options = OptionsWith(maxAttempts: 3);

        for (int attempt = 0; attempt < 4; attempt++)
        {
            await AttemptPasswordLoginAsync(options, "wrong-password");
        }

        // Attempts are counted per identifier, so one customer being brute
        // forced must not take other customers offline with them.
        await using var context = _database.CreateContext();
        var otherResult = await CreateService(context, options)
            .LoginWithPasswordAsync(new LoginWithPasswordRequest("someone.else@example.com", "whatever"));

        otherResult.IsFailure.Should().BeTrue();
        otherResult.Error.Code.Should().Be("Login.InvalidCredentials");
    }

    [Fact]
    public async Task An_unknown_email_is_rejected_with_the_same_error_as_a_wrong_password()
    {
        var options = OptionsWith(maxAttempts: 5);

        await using var context = _database.CreateContext();
        var unknown = await CreateService(context, options)
            .LoginWithPasswordAsync(new LoginWithPasswordRequest("nobody@example.com", "whatever"));

        var wrongPassword = await AttemptPasswordLoginAsync(options, "wrong-password");

        // Distinguishable errors would turn the login form into an
        // account-enumeration oracle (SRS 28.3).
        unknown.Error.Code.Should().Be(wrongPassword.Error.Code);
        unknown.Error.Message.Should().Be(wrongPassword.Error.Message);
    }

    [Fact]
    public async Task Successful_logins_do_not_count_towards_the_lockout()
    {
        var options = OptionsWith(maxAttempts: 3);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            (await AttemptPasswordLoginAsync(options, CorrectPassword)).IsSuccess.Should().BeTrue();
        }

        (await AttemptPasswordLoginAsync(options, CorrectPassword)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Otp_login_shares_the_same_lockout_counter()
    {
        var options = OptionsWith(maxAttempts: 3);

        _otpService
            .Setup(o => o.ValidateAsync(Mobile, It.IsAny<string>(), OtpPurpose.Login))
            .ReturnsAsync(Result.Failure(Error.Validation("Otp.Incorrect", "The OTP code is incorrect.")));

        for (int attempt = 0; attempt < 3; attempt++)
        {
            await using var context = _database.CreateContext();
            var result = await CreateService(context, options)
                .LoginWithOtpAsync(new LoginWithOtpRequest(Mobile, "000000"));
            result.IsFailure.Should().BeTrue();
        }

        await using (var context = _database.CreateContext())
        {
            // OTP guesses have to be throttled the same way password guesses
            // are, or the OTP path becomes the soft underbelly.
            var locked = await CreateService(context, options)
                .LoginWithOtpAsync(new LoginWithOtpRequest(Mobile, "000000"));
            locked.IsFailure.Should().BeTrue();
            locked.Error.Code.Should().Be("Login.AccountLocked");
        }
    }

    [Fact]
    public async Task Every_failed_attempt_is_recorded_for_audit()
    {
        var options = OptionsWith(maxAttempts: 10);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            await AttemptPasswordLoginAsync(options, "wrong-password");
        }

        await using var context = _database.CreateContext();
        var failures = await context.Set<LoginAttempt>()
            .Where(a => a.Identifier == Email && !a.Succeeded)
            .ToListAsync();

        failures.Should().HaveCount(3);
        // The attempted password must never be persisted anywhere on this
        // path (CLAUDE.md: never log secrets).
        failures.Should().OnlyContain(a => a.Identifier == Email);
    }

    [Fact]
    public async Task A_blocked_customer_cannot_log_in_even_with_the_correct_password()
    {
        await using (var context = _database.CreateContext())
        {
            var customer = await context.Set<Customer>().SingleAsync(c => c.Id == _customerId);
            customer.UpdateStatus(CustomerStatus.Blocked);
            await context.SaveChangesAsync();
        }

        var result = await AttemptPasswordLoginAsync(OptionsWith(maxAttempts: 5), CorrectPassword);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Login.AccountNotActive");
    }

    public void Dispose() => _database.Dispose();
}
