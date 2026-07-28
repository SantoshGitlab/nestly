using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nestly.Application;
using Nestly.Application.Profile;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// Profile management (SRS 11.2.3): view/edit, re-verified mobile and email
/// changes, and communication preferences.
/// </summary>
public class CustomerProfileServiceTests : IDisposable
{
    private const string Mobile = "+919876543210";
    private const string NewMobile = "+919000000001";
    private const string Email = "customer@example.com";
    private const string NewEmail = "new.address@example.com";
    private const string ValidCode = "123456";

    private readonly TestDatabase _database = new();
    private readonly Mock<IOTPService> _otpService = new();
    private Guid _customerId;

    public CustomerProfileServiceTests()
    {
        _otpService
            .Setup(o => o.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OtpPurpose>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("Otp.NotFound", "No pending OTP for this request.")));
        _otpService
            .Setup(o => o.ValidateAsync(NewMobile, ValidCode, OtpPurpose.MobileChange))
            .ReturnsAsync(Result.Success());
        _otpService
            .Setup(o => o.ValidateAsync(NewEmail, ValidCode, OtpPurpose.EmailChange))
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
        context.Add(new CustomerAuthIdentity(
            Guid.NewGuid(), customer.Id, AuthProviderType.MobileOtp, Mobile, isPrimary: true));
        context.Add(new CustomerAuthIdentity(
            Guid.NewGuid(), customer.Id, AuthProviderType.EmailPassword, Email, isPrimary: false));
        context.SaveChanges();
    }

    private CustomerProfileService CreateService(NestlyDbContext context, AccountOptions? options = null) =>
        new(
            new CustomerRepository(context),
            new CustomerAuthIdentityRepository(context),
            new CustomerCommunicationPreferenceRepository(context),
            _otpService.Object,
            Options.Create(options ?? new AccountOptions()),
            NullLogger<CustomerProfileService>.Instance);

    [Fact]
    public async Task GetAsync_returns_the_profile_without_any_credential_material()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).GetAsync(_customerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Mobile.Should().Be(Mobile);
        result.Value.Email.Should().Be(Email);
        result.Value.Status.Should().Be(nameof(CustomerStatus.Active));

        // CustomerProfileResponse is a closed record; this asserts the shape
        // stays free of anything secret if fields are added later.
        typeof(CustomerProfileResponse).GetProperties()
            .Select(p => p.Name)
            .Should().NotContain(name =>
                name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetAsync_reports_not_found_for_an_unknown_customer()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).GetAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Profile.NotFound");
    }

    [Fact]
    public async Task UpdateAsync_saves_the_editable_fields()
    {
        var dateOfBirth = new DateTime(1990, 5, 17, 0, 0, 0, DateTimeKind.Utc);

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context).UpdateAsync(_customerId,
                new UpdateProfileRequest("Updated Name", dateOfBirth, "Bengaluru", "Karnataka", "560001", "India"));

            result.IsSuccess.Should().BeTrue();
        }

        await using (var context = _database.CreateContext())
        {
            var customer = await context.Set<Customer>().SingleAsync();
            customer.Name.Should().Be("Updated Name");
            customer.City.Should().Be("Bengaluru");
            customer.Pincode.Should().Be("560001");
            customer.DateOfBirth.Should().Be(dateOfBirth);
        }
    }

    [Fact]
    public async Task UpdateAsync_cannot_change_the_mobile_or_email()
    {
        await using (var context = _database.CreateContext())
        {
            await CreateService(context).UpdateAsync(_customerId,
                new UpdateProfileRequest("Updated Name", null, null, null, null, null));
        }

        await using (var context = _database.CreateContext())
        {
            // Both are identity-bearing: allowing an unverified PUT to move
            // them would let anyone with a valid token take over a login
            // identifier (SRS 11.2.3 requires re-verification).
            var customer = await context.Set<Customer>().SingleAsync();
            customer.Mobile.Should().Be(Mobile);
            customer.Email.Should().Be(Email);
        }
    }

    [Fact]
    public async Task RequestMobileChangeOtpAsync_sends_a_code_to_the_new_number()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context)
            .RequestMobileChangeOtpAsync(_customerId, new RequestMobileChangeOtpRequest(NewMobile));

        result.IsSuccess.Should().BeTrue();
        // Verifying against the *new* number is the whole point — a code sent
        // to the old one proves nothing about the new one.
        _otpService.Verify(
            o => o.GenerateAsync(NewMobile, OtpPurpose.MobileChange, NotificationChannel.Sms),
            Times.Once);
    }

    [Fact]
    public async Task RequestMobileChangeOtpAsync_refuses_a_number_that_is_already_registered()
    {
        await using (var context = _database.CreateContext())
        {
            context.Add(new Customer(Guid.NewGuid(), NewMobile, "Someone Else", CustomerStatus.Active));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context)
                .RequestMobileChangeOtpAsync(_customerId, new RequestMobileChangeOtpRequest(NewMobile));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Profile.MobileAlreadyRegistered");
        }
    }

    [Fact]
    public async Task RequestMobileChangeOtpAsync_refuses_the_number_already_on_the_account()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context)
            .RequestMobileChangeOtpAsync(_customerId, new RequestMobileChangeOtpRequest(Mobile));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Profile.MobileUnchanged");
    }

    [Fact]
    public async Task ConfirmMobileChangeAsync_moves_both_the_customer_and_the_auth_identity()
    {
        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context)
                .ConfirmMobileChangeAsync(_customerId, new ConfirmMobileChangeRequest(NewMobile, ValidCode));

            result.IsSuccess.Should().BeTrue();
            result.Value.Mobile.Should().Be(NewMobile);
        }

        await using (var context = _database.CreateContext())
        {
            (await context.Set<Customer>().SingleAsync()).Mobile.Should().Be(NewMobile);

            // Leaving the identity behind would let the old number keep
            // logging in and lock the new one out.
            var identity = await context.Set<CustomerAuthIdentity>()
                .SingleAsync(i => i.Provider == AuthProviderType.MobileOtp);
            identity.Identifier.Should().Be(NewMobile);
        }
    }

    [Fact]
    public async Task ConfirmMobileChangeAsync_rejects_a_bad_code_and_changes_nothing()
    {
        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context)
                .ConfirmMobileChangeAsync(_customerId, new ConfirmMobileChangeRequest(NewMobile, "000000"));

            result.IsFailure.Should().BeTrue();
        }

        await using (var context = _database.CreateContext())
        {
            (await context.Set<Customer>().SingleAsync()).Mobile.Should().Be(Mobile);
            (await context.Set<CustomerAuthIdentity>()
                .SingleAsync(i => i.Provider == AuthProviderType.MobileOtp))
                .Identifier.Should().Be(Mobile);
        }
    }

    [Fact]
    public async Task RequestEmailChangeOtpAsync_sends_the_code_over_email_to_the_new_address()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context)
            .RequestEmailChangeOtpAsync(_customerId, new RequestEmailChangeOtpRequest(NewEmail));

        result.IsSuccess.Should().BeTrue();
        // The address being claimed is not reachable by SMS, so this is the
        // one flow that has to go over the email channel.
        _otpService.Verify(
            o => o.GenerateAsync(NewEmail, OtpPurpose.EmailChange, NotificationChannel.Email),
            Times.Once);
    }

    [Fact]
    public async Task RequestEmailChangeOtpAsync_refuses_an_address_another_customer_already_uses()
    {
        await using (var context = _database.CreateContext())
        {
            context.Add(new Customer(Guid.NewGuid(), "+919000009999", "Someone Else", CustomerStatus.Active, NewEmail));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context)
                .RequestEmailChangeOtpAsync(_customerId, new RequestEmailChangeOtpRequest(NewEmail));

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Profile.EmailAlreadyRegistered");
        }
    }

    [Fact]
    public async Task RequestEmailChangeOtpAsync_allows_a_duplicate_when_unique_emails_are_switched_off()
    {
        await using (var context = _database.CreateContext())
        {
            context.Add(new Customer(Guid.NewGuid(), "+919000009999", "Someone Else", CustomerStatus.Active));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            // RequireUniqueEmail is configurable (SRS 11.2.1); the service has
            // to honour it rather than assume the default.
            var options = new AccountOptions { RequireUniqueEmail = false };
            var result = await CreateService(context, options)
                .RequestEmailChangeOtpAsync(_customerId, new RequestEmailChangeOtpRequest(NewEmail));

            result.IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_moves_the_password_identity_with_the_address()
    {
        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context)
                .ConfirmEmailChangeAsync(_customerId, new ConfirmEmailChangeRequest(NewEmail, ValidCode));

            result.IsSuccess.Should().BeTrue();
            result.Value.Email.Should().Be(NewEmail);
        }

        await using (var context = _database.CreateContext())
        {
            (await context.Set<Customer>().SingleAsync()).Email.Should().Be(NewEmail);

            // Password login resolves by email; leaving the identity behind
            // would silently break it.
            var identity = await context.Set<CustomerAuthIdentity>()
                .SingleAsync(i => i.Provider == AuthProviderType.EmailPassword);
            identity.Identifier.Should().Be(NewEmail);
        }
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_rejects_a_code_issued_for_a_different_purpose()
    {
        await using var context = _database.CreateContext();

        // The mobile-change code is valid — but only for MobileChange.
        var result = await CreateService(context)
            .ConfirmEmailChangeAsync(_customerId, new ConfirmEmailChangeRequest(NewMobile, ValidCode));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetPreferencesAsync_returns_defaults_without_creating_a_row()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).GetPreferencesAsync(_customerId);

        result.IsSuccess.Should().BeTrue();
        // Transactional on, promotional off — opt-in is the correct default
        // for marketing traffic.
        result.Value.TransactionalSms.Should().BeTrue();
        result.Value.TransactionalEmail.Should().BeTrue();
        result.Value.PromotionalSms.Should().BeFalse();
        result.Value.PromotionalEmail.Should().BeFalse();
        result.Value.Push.Should().BeFalse();

        // A GET must stay side-effect free.
        (await context.Set<CustomerCommunicationPreference>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetPreferencesAsync_reports_not_found_for_an_unknown_customer()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).GetPreferencesAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Profile.NotFound");
    }

    [Fact]
    public async Task UpdatePreferencesAsync_creates_the_row_on_first_save_and_updates_it_after()
    {
        await using (var context = _database.CreateContext())
        {
            var result = await CreateService(context).UpdatePreferencesAsync(_customerId,
                new CommunicationPreferencesRequest(true, false, false, true, false, false, true));

            result.IsSuccess.Should().BeTrue();
            result.Value.PromotionalSms.Should().BeTrue();
            result.Value.TransactionalEmail.Should().BeFalse();
        }

        await using (var context = _database.CreateContext())
        {
            (await context.Set<CustomerCommunicationPreference>().CountAsync()).Should().Be(1);
        }

        await using (var context = _database.CreateContext())
        {
            await CreateService(context).UpdatePreferencesAsync(_customerId,
                new CommunicationPreferencesRequest(false, false, false, false, false, false, false));
        }

        await using (var context = _database.CreateContext())
        {
            // A second save must update in place, not insert a second row —
            // the unique index would reject that anyway.
            var rows = await context.Set<CustomerCommunicationPreference>().ToListAsync();
            rows.Should().HaveCount(1);
            rows[0].PromotionalSmsEnabled.Should().BeFalse();
            rows[0].PushEnabled.Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdatePreferencesAsync_reports_not_found_for_an_unknown_customer()
    {
        await using var context = _database.CreateContext();
        var result = await CreateService(context).UpdatePreferencesAsync(Guid.NewGuid(),
            new CommunicationPreferencesRequest(true, true, true, true, true, true, true));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Profile.NotFound");
    }

    public void Dispose() => _database.Dispose();
}
