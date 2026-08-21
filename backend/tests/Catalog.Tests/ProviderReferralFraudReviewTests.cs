using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers PROVIDER-REFERRAL.md's manual fraud review queue (flag/approve/reject), mirrors ReferralFraudReviewTests.</summary>
public sealed class ProviderReferralFraudReviewTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ProviderReferralFraudReviewTests(TestDatabase db) => _db = db;

    private static ProviderReferralFraudReviewService BuildFraudService(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(new ProviderReferralRepository(context));

    private static Provider SeedProvider(Nestly.Infrastructure.Persistence.NestlyDbContext context, string name)
    {
        var provider = new Provider(Guid.NewGuid(), name, name, ProviderType.Individual, "+9198" + Guid.NewGuid().ToString("N")[..8]);
        context.Add(provider);
        context.SaveChanges();
        return provider;
    }

    private static ProviderReferralProgramConfig SeedConfig(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        context.RemoveRange(context.ProviderReferralProgramConfigs);
        context.SaveChanges();
        var config = new ProviderReferralProgramConfig(Guid.NewGuid(), 500m, 500m, 3, 45, null, isActive: true);
        context.Add(config);
        context.SaveChanges();
        return config;
    }

    private static ProviderReferral SeedRewardedReferral(
        Nestly.Infrastructure.Persistence.NestlyDbContext context, Provider referrer, Provider referee, ProviderReferralProgramConfig config, Guid qualifyingBookingId)
    {
        var referral = new ProviderReferral(Guid.NewGuid(), referrer.Id, referee.Id, "TESTCODE", config);
        referral.MarkQualified(qualifyingBookingId);
        referral.MarkRewarded(Guid.NewGuid(), Guid.NewGuid());
        context.Add(referral);
        context.SaveChanges();
        return referral;
    }

    [Fact]
    public async Task FlagAsync_flags_a_referral_without_touching_its_status()
    {
        using var context = _db.CreateContext();
        var config = SeedConfig(context);
        var referrer = SeedProvider(context, "Referrer");
        var referee = SeedProvider(context, "Referee");
        var referral = SeedRewardedReferral(context, referrer, referee, config, Guid.NewGuid());

        var adminId = Guid.NewGuid();
        var result = await BuildFraudService(context).FlagAsync(referral.Id, adminId, "Same device as referrer");

        result.IsSuccess.Should().BeTrue();
        var updated = context.ProviderReferrals.Single(r => r.Id == referral.Id);
        updated.IsFraudFlagged.Should().BeTrue();
        updated.FraudReviewNote.Should().Be("Same device as referrer");
        updated.FraudReviewedByAdminUserId.Should().Be(adminId);
        updated.Status.Should().Be(ProviderReferralStatus.Rewarded, "flagging is independent of the referral's lifecycle status");
    }

    [Fact]
    public async Task ApproveAsync_clears_the_flag_and_records_the_admins_note()
    {
        using var context = _db.CreateContext();
        var config = SeedConfig(context);
        var referrer = SeedProvider(context, "Referrer");
        var referee = SeedProvider(context, "Referee");
        var referral = SeedRewardedReferral(context, referrer, referee, config, Guid.NewGuid());

        var flagAdminId = Guid.NewGuid();
        await BuildFraudService(context).FlagAsync(referral.Id, flagAdminId, "Suspicious pattern");

        var approveAdminId = Guid.NewGuid();
        var result = await BuildFraudService(context).ApproveAsync(referral.Id, approveAdminId, "Confirmed with support ticket #42");

        result.IsSuccess.Should().BeTrue();
        var updated = context.ProviderReferrals.Single(r => r.Id == referral.Id);
        updated.IsFraudFlagged.Should().BeFalse();
        updated.FraudReviewNote.Should().Contain("Confirmed with support ticket #42");
        updated.FraudReviewedByAdminUserId.Should().Be(approveAdminId);
    }

    [Fact]
    public async Task RejectAsync_clears_the_flag_as_a_false_positive()
    {
        using var context = _db.CreateContext();
        var config = SeedConfig(context);
        var referrer = SeedProvider(context, "Referrer");
        var referee = SeedProvider(context, "Referee");
        var referral = SeedRewardedReferral(context, referrer, referee, config, Guid.NewGuid());

        await BuildFraudService(context).FlagAsync(referral.Id, Guid.NewGuid(), "Looked suspicious");

        var rejectAdminId = Guid.NewGuid();
        var result = await BuildFraudService(context).RejectAsync(referral.Id, rejectAdminId, null);

        result.IsSuccess.Should().BeTrue();
        var updated = context.ProviderReferrals.Single(r => r.Id == referral.Id);
        updated.IsFraudFlagged.Should().BeFalse();
        updated.FraudReviewNote.Should().Contain("False positive");
    }

    [Fact]
    public async Task ApproveAsync_fails_when_the_referral_is_not_currently_flagged()
    {
        using var context = _db.CreateContext();
        var config = SeedConfig(context);
        var referrer = SeedProvider(context, "Referrer");
        var referee = SeedProvider(context, "Referee");
        var referral = SeedRewardedReferral(context, referrer, referee, config, Guid.NewGuid());

        var result = await BuildFraudService(context).ApproveAsync(referral.Id, Guid.NewGuid(), null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProviderReferral.NotFlagged");
    }

    [Fact]
    public async Task FlagAsync_fails_for_a_nonexistent_referral()
    {
        using var context = _db.CreateContext();

        var result = await BuildFraudService(context).FlagAsync(Guid.NewGuid(), Guid.NewGuid(), null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProviderReferral.NotFound");
    }
}
