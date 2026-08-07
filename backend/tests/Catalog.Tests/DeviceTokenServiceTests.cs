using FluentAssertions;
using Nestly.Application;
using Nestly.Application.Notifications;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 156 (registration half): device token registration for push delivery.</summary>
public sealed class DeviceTokenServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public DeviceTokenServiceTests(TestDatabase db) => _db = db;

    private static DeviceTokenService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(new DeviceTokenRepository(context));

    private Guid SeedCustomer(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Asha Rao", CustomerStatus.Active);
        context.Add(customer);
        context.SaveChanges();
        return customer.Id;
    }

    private Guid SeedProvider(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "9" + Guid.NewGuid().ToString("N")[..9]);
        context.Add(provider);
        context.SaveChanges();
        return provider.Id;
    }

    /// <summary>
    /// Task 277: a provider now has its own device tokens, and the two id
    /// spaces are strangers even when they collide - a provider-owned
    /// registration must never surface in a customer's list.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_registers_a_token_for_a_provider_owner_and_keeps_it_out_of_the_customers_list()
    {
        Guid providerId, customerId;
        using (var context = _db.CreateContext())
        {
            providerId = SeedProvider(context);
            customerId = SeedCustomer(context);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).RegisterAsync(DeviceTokenOwner.ForProvider(providerId), new RegisterDeviceTokenRequest(DevicePlatform.Fcm, "token-" + Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();

        using var readContext = _db.CreateContext();
        var providerTokens = await BuildService(readContext).ListAsync(DeviceTokenOwner.ForProvider(providerId));
        var customerTokens = await BuildService(readContext).ListAsync(DeviceTokenOwner.ForCustomer(customerId));

        providerTokens.Value.Should().ContainSingle();
        customerTokens.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterAsync_creates_a_new_active_token()
    {
        Guid customerId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).RegisterAsync(DeviceTokenOwner.ForCustomer(customerId), new RegisterDeviceTokenRequest(DevicePlatform.Fcm, "token-" + Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        result.Value.Platform.Should().Be(DevicePlatform.Fcm);
    }

    [Fact]
    public async Task RegisterAsync_reactivates_an_existing_revoked_token_for_the_same_customer()
    {
        Guid customerId;
        string token = "token-" + Guid.NewGuid();
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
        }

        Guid tokenId;
        using (var context = _db.CreateContext())
        {
            var registered = await BuildService(context).RegisterAsync(DeviceTokenOwner.ForCustomer(customerId), new RegisterDeviceTokenRequest(DevicePlatform.Fcm, token));
            tokenId = registered.Value.Id;
        }

        using (var context = _db.CreateContext())
        {
            var revoked = await BuildService(context).RevokeAsync(DeviceTokenOwner.ForCustomer(customerId), tokenId);
            revoked.IsSuccess.Should().BeTrue();
        }

        using var reRegisterContext = _db.CreateContext();
        var result = await BuildService(reRegisterContext).RegisterAsync(DeviceTokenOwner.ForCustomer(customerId), new RegisterDeviceTokenRequest(DevicePlatform.Fcm, token));

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(tokenId, "the same device row is reactivated, not duplicated");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_reassigns_a_token_already_owned_by_a_different_customer()
    {
        Guid customerAId, customerBId;
        string token = "token-" + Guid.NewGuid();
        using (var context = _db.CreateContext())
        {
            customerAId = SeedCustomer(context);
            customerBId = SeedCustomer(context);
        }

        using (var context = _db.CreateContext())
        {
            await BuildService(context).RegisterAsync(DeviceTokenOwner.ForCustomer(customerAId), new RegisterDeviceTokenRequest(DevicePlatform.Fcm, token));
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).RegisterAsync(DeviceTokenOwner.ForCustomer(customerBId), new RegisterDeviceTokenRequest(DevicePlatform.Apns, token));

        result.IsSuccess.Should().BeTrue();

        using var readContext = _db.CreateContext();
        var listA = await BuildService(readContext).ListAsync(DeviceTokenOwner.ForCustomer(customerAId));
        var listB = await BuildService(readContext).ListAsync(DeviceTokenOwner.ForCustomer(customerBId));
        listA.Value.Should().BeEmpty("the device now belongs to customer B");
        listB.Value.Should().ContainSingle(t => t.Token == token && t.Platform == DevicePlatform.Apns);
    }

    [Fact]
    public async Task RevokeAsync_rejects_a_token_owned_by_another_customer()
    {
        Guid ownerId, otherId;
        using (var context = _db.CreateContext())
        {
            ownerId = SeedCustomer(context);
            otherId = SeedCustomer(context);
        }

        Guid tokenId;
        using (var context = _db.CreateContext())
        {
            var registered = await BuildService(context).RegisterAsync(DeviceTokenOwner.ForCustomer(ownerId), new RegisterDeviceTokenRequest(DevicePlatform.Fcm, "token-" + Guid.NewGuid()));
            tokenId = registered.Value.Id;
        }

        using var context2 = _db.CreateContext();
        var result = await BuildService(context2).RevokeAsync(DeviceTokenOwner.ForCustomer(otherId), tokenId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DeviceToken.NotFound");
    }

    [Fact]
    public async Task ListAsync_excludes_revoked_tokens()
    {
        Guid customerId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
        }

        Guid tokenId;
        using (var context = _db.CreateContext())
        {
            var registered = await BuildService(context).RegisterAsync(DeviceTokenOwner.ForCustomer(customerId), new RegisterDeviceTokenRequest(DevicePlatform.Fcm, "token-" + Guid.NewGuid()));
            tokenId = registered.Value.Id;
        }

        using (var context = _db.CreateContext())
        {
            await BuildService(context).RevokeAsync(DeviceTokenOwner.ForCustomer(customerId), tokenId);
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).ListAsync(DeviceTokenOwner.ForCustomer(customerId));

        result.Value.Should().BeEmpty();
    }
}
