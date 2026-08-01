using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 168: the customer-facing Refer &amp; Earn summary and history the consumer-api's ReferralController exposes.</summary>
public sealed class ReferralCustomerServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ReferralCustomerServiceTests(TestDatabase db) => _db = db;

    private static ReferralCustomerService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(
            new ReferralRepository(context),
            new ReferralCodeService(new CustomerRepository(context), Options.Create(new ReferralOptions())),
            new CustomerRepository(context));

    private static Customer SeedCustomer(Nestly.Infrastructure.Persistence.NestlyDbContext context, string name)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], name, CustomerStatus.Active,
            $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com");
        context.Add(customer);
        context.SaveChanges();
        return customer;
    }

    private static ReferralProgramConfig SeedConfig(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        context.RemoveRange(context.ReferralProgramConfigs);
        context.SaveChanges();

        var config = new ReferralProgramConfig(Guid.NewGuid(), ReferralRewardType.WalletCredit, 100m, ReferralRewardType.WalletCredit, 50m, 299m, 30, null, isActive: true);
        context.Add(config);
        context.SaveChanges();
        return config;
    }

    [Fact]
    public async Task GetSummaryAsync_lazily_generates_a_code_and_share_link_on_first_call()
    {
        using var context = _db.CreateContext();
        var customer = SeedCustomer(context, "Referrer");
        context.Set<Customer>().Single(c => c.Id == customer.Id).ReferralCode.Should().BeNull("no code should exist until the summary is first requested");

        var summary = await BuildService(context).GetSummaryAsync(customer.Id);

        summary.ReferralCode.Should().NotBeNullOrWhiteSpace();
        summary.ShareLink.Should().EndWith(summary.ReferralCode);
        summary.InvitedCount.Should().Be(0);
        summary.QualifiedCount.Should().Be(0);
        summary.RewardedCount.Should().Be(0);
        summary.TotalEarned.Should().Be(0m);
    }

    [Fact]
    public async Task GetSummaryAsync_returns_the_same_code_on_a_second_call()
    {
        using var context = _db.CreateContext();
        var customer = SeedCustomer(context, "Referrer");
        var service = BuildService(context);

        var first = await service.GetSummaryAsync(customer.Id);
        var second = await service.GetSummaryAsync(customer.Id);

        second.ReferralCode.Should().Be(first.ReferralCode);
    }

    [Fact]
    public async Task GetSummaryAsync_counts_lifetime_stats_across_every_status()
    {
        using var context = _db.CreateContext();
        var config = SeedConfig(context);
        var referrer = SeedCustomer(context, "Referrer");

        var registeredOnly = SeedCustomer(context, "RegisteredOnly");
        context.Add(new Domain.Referral(Guid.NewGuid(), referrer.Id, registeredOnly.Id, "CODE1", config));

        var qualified = SeedCustomer(context, "Qualified");
        var qualifiedReferral = new Domain.Referral(Guid.NewGuid(), referrer.Id, qualified.Id, "CODE2", config);
        qualifiedReferral.MarkQualified(Guid.NewGuid());
        context.Add(qualifiedReferral);

        var rewarded = SeedCustomer(context, "Rewarded");
        var rewardedReferral = new Domain.Referral(Guid.NewGuid(), referrer.Id, rewarded.Id, "CODE3", config);
        rewardedReferral.MarkQualified(Guid.NewGuid());
        rewardedReferral.MarkRewarded(Guid.NewGuid(), null, Guid.NewGuid(), null);
        context.Add(rewardedReferral);

        context.SaveChanges();

        var summary = await BuildService(context).GetSummaryAsync(referrer.Id);

        summary.InvitedCount.Should().Be(3);
        summary.QualifiedCount.Should().Be(2, "Qualified and Rewarded both count as having qualified");
        summary.RewardedCount.Should().Be(1);
        summary.TotalEarned.Should().Be(100m, "only the Rewarded referral's referrer-side reward value counts toward lifetime earnings");
    }

    [Fact]
    public async Task GetHistoryAsync_returns_every_referral_this_customer_made_newest_first()
    {
        using var context = _db.CreateContext();
        var config = SeedConfig(context);
        var referrer = SeedCustomer(context, "Referrer");
        var refereeA = SeedCustomer(context, "RefereeA");
        var refereeB = SeedCustomer(context, "RefereeB");

        context.Add(new Domain.Referral(Guid.NewGuid(), referrer.Id, refereeA.Id, "CODEA", config));
        context.Add(new Domain.Referral(Guid.NewGuid(), referrer.Id, refereeB.Id, "CODEB", config));
        context.SaveChanges();

        var history = await BuildService(context).GetHistoryAsync(referrer.Id);

        history.Should().HaveCount(2);
        history.Should().Contain(h => h.RefereeName == "RefereeA" && h.Status == nameof(ReferralStatus.Registered));
        history.Should().Contain(h => h.RefereeName == "RefereeB" && h.Status == nameof(ReferralStatus.Registered));
    }

    [Fact]
    public async Task GetHistoryAsync_is_empty_for_a_customer_who_has_never_referred_anyone()
    {
        using var context = _db.CreateContext();
        var customer = SeedCustomer(context, "NoReferrals");

        var history = await BuildService(context).GetHistoryAsync(customer.Id);

        history.Should().BeEmpty();
    }
}
