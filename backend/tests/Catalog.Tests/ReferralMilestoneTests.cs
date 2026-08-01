using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Nestly.Application;
using Nestly.Application.Notifications;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 174: the milestone bonus disbursed on top of the per-referral
/// reward once a referrer's rewarded-referral count crosses a configured
/// threshold, and its idempotency guard (the <see cref="ReferralMilestoneAward"/>
/// row) - a milestone must never be paid twice for the same referrer, even if
/// the disbursement path runs more than once (e.g. a retried event handler).
/// Also covers task 172: the ReferralRewardCredited/ReferralRegistered
/// notification dispatch actually resolves a template and sends
/// successfully now that NotificationTemplateSeedData carries rows for them.
/// </summary>
public sealed class ReferralMilestoneTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ReferralMilestoneTests(TestDatabase db) => _db = db;

    private static ReferralRewardService BuildRewardService(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(
            new ReferralRepository(context),
            new ReferralProgramConfigRepository(context),
            new CustomerRepository(context),
            new WalletService(new WalletLedgerRepository(context)),
            new CouponRepository(context),
            new ReferralMilestoneRepository(context),
            new ReferralMilestoneAwardRepository(context),
            new NotificationDispatchService(
                new NotificationTemplateRenderer(new FakeNotificationTemplateRepository(), new MemoryCache(new MemoryCacheOptions())),
                new SandboxNotificationProvider(NullLogger<SandboxNotificationProvider>.Instance),
                new SandboxPushNotificationProvider(NullLogger<SandboxPushNotificationProvider>.Instance),
                new NotificationEventRepository(context),
                new NoOpMetricsService(),
                NullLogger<NotificationDispatchService>.Instance),
            NullLogger<ReferralRewardService>.Instance);

    private static ReferralQualifyingBookingHandler BuildHandler(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(
            new BookingRepository(context),
            new ReferralRepository(context),
            BuildRewardService(context),
            NullLogger<ReferralQualifyingBookingHandler>.Instance);

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

        var config = new ReferralProgramConfig(
            Guid.NewGuid(), ReferralRewardType.WalletCredit, 100m, ReferralRewardType.WalletCredit, 50m,
            299m, 30, null, isActive: true);
        context.Add(config);
        context.SaveChanges();
        return config;
    }

    private static ReferralMilestone SeedMilestone(Nestly.Infrastructure.Persistence.NestlyDbContext context, int threshold, decimal bonusValue)
    {
        var milestone = new ReferralMilestone(Guid.NewGuid(), threshold, ReferralRewardType.WalletCredit, bonusValue, isActive: true);
        context.Add(milestone);
        context.SaveChanges();
        return milestone;
    }

    private static Domain.Referral SeedRegisteredReferral(
        Nestly.Infrastructure.Persistence.NestlyDbContext context, Customer referrer, Customer referee, ReferralProgramConfig config)
    {
        var referral = new Domain.Referral(Guid.NewGuid(), referrer.Id, referee.Id, "TESTCODE" + Guid.NewGuid().ToString("N")[..6], config);
        context.Add(referral);
        context.SaveChanges();
        return referral;
    }

    private static Booking SeedCompletedBooking(Nestly.Infrastructure.Persistence.NestlyDbContext context, Guid customerId, decimal totalPayable)
    {
        var booking = new Booking(
            Guid.NewGuid(), customerId,
            new CustomerSnapshot("Test Customer", "9" + Guid.NewGuid().ToString("N")[..9]),
            null,
            new AddressSnapshot("Home", "123 St", null, null, "560001", "Bengaluru", "Karnataka", 12.9m, 77.5m, "Test", "9000000000"),
            new SlotSnapshot(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13)),
            new PriceSnapshot(totalPayable, 1, totalPayable, 0, 0, totalPayable, 0, 0, 0, totalPayable));

        booking.TransitionTo(BookingStatus.PaymentPending);
        booking.TransitionTo(BookingStatus.Confirmed);
        booking.TransitionTo(BookingStatus.AwaitingFulfilment);
        booking.TransitionTo(BookingStatus.Assigned);
        booking.TransitionTo(BookingStatus.InProgress);
        booking.TransitionTo(BookingStatus.Completed);

        context.Add(booking);
        context.SaveChanges();
        return booking;
    }

    private static DomainEventNotification<BookingStatusChangedEvent> CompletionNotification(Guid bookingId) =>
        new(new BookingStatusChangedEvent(bookingId, BookingStatus.InProgress, BookingStatus.Completed));

    [Fact]
    public async Task Milestone_bonus_is_disbursed_once_the_referrer_first_crosses_the_threshold()
    {
        using var context = _db.CreateContext();
        var config = SeedConfig(context);
        var milestone = SeedMilestone(context, threshold: 1, bonusValue: 200m);
        var referrer = SeedCustomer(context, "Referrer");
        var referee = SeedCustomer(context, "Referee");
        var referral = SeedRegisteredReferral(context, referrer, referee, config);
        var booking = SeedCompletedBooking(context, referee.Id, totalPayable: 500m);

        await BuildHandler(context).Handle(CompletionNotification(booking.Id), CancellationToken.None);

        context.ReferralMilestoneAwards.Count(a => a.ReferralMilestoneId == milestone.Id && a.ReferrerCustomerId == referrer.Id).Should().Be(1);

        // 100 (per-referral reward) + 200 (milestone bonus) = 300.
        var balance = context.WalletLedgerEntries
            .Where(e => e.CustomerId == referrer.Id)
            .OrderByDescending(e => e.CreatedAtUtc)
            .First().BalanceAfter;
        balance.Should().Be(300m);

        context.WalletLedgerEntries.Should().ContainSingle(e => e.CustomerId == referrer.Id && e.SourceType == WalletSourceType.ReferralMilestoneBonus && e.Amount == 200m);
    }

    [Fact]
    public async Task Milestone_bonus_is_never_disbursed_twice_for_the_same_referrer_and_milestone()
    {
        using var context = _db.CreateContext();
        var config = SeedConfig(context);
        // Threshold 2 (not 1) - distinct from the previous test's milestone
        // row, since ThresholdCount is globally unique (this shared
        // TestDatabase spans every test in the class). Two referrals are
        // completed below purely to reach that count; the pre-seeded award
        // means neither should actually pay a bonus.
        var milestone = SeedMilestone(context, threshold: 2, bonusValue: 200m);
        var referrer = SeedCustomer(context, "Referrer");

        // Simulates the milestone having already been paid earlier (e.g. a
        // prior run, or a handler retry that already completed) - the award
        // row is the idempotency guard, independent of anything else in the
        // request that's about to run.
        context.Add(new ReferralMilestoneAward(Guid.NewGuid(), milestone.Id, referrer.Id, walletEntryId: Guid.NewGuid(), couponId: null));
        context.SaveChanges();

        var firstReferee = SeedCustomer(context, "Referee1");
        SeedRegisteredReferral(context, referrer, firstReferee, config);
        await BuildHandler(context).Handle(CompletionNotification(SeedCompletedBooking(context, firstReferee.Id, 500m).Id), CancellationToken.None);

        var secondReferee = SeedCustomer(context, "Referee2");
        SeedRegisteredReferral(context, referrer, secondReferee, config);
        await BuildHandler(context).Handle(CompletionNotification(SeedCompletedBooking(context, secondReferee.Id, 500m).Id), CancellationToken.None);

        context.ReferralMilestoneAwards.Count(a => a.ReferralMilestoneId == milestone.Id && a.ReferrerCustomerId == referrer.Id)
            .Should().Be(1, "the milestone was already paid - crossing the threshold again must not pay it a second time");

        // Only the two per-referral rewards (100 each) should have landed -
        // no milestone bonus credit from this run.
        context.WalletLedgerEntries.Count(e => e.CustomerId == referrer.Id && e.SourceType == WalletSourceType.ReferralMilestoneBonus)
            .Should().Be(0, "no NEW milestone wallet credit should have been issued by this run");
    }

    [Fact]
    public async Task Milestone_bonus_only_fires_for_milestones_matching_the_exact_new_count()
    {
        using var context = _db.CreateContext();
        var config = SeedConfig(context);
        var milestone = SeedMilestone(context, threshold: 3, bonusValue: 500m);
        var referrer = SeedCustomer(context, "Referrer");

        // First two referrals bring the rewarded count to 1, then 2 -
        // neither matches threshold 3, so no bonus yet.
        var firstReferee = SeedCustomer(context, "FirstReferee");
        SeedRegisteredReferral(context, referrer, firstReferee, config);
        await BuildHandler(context).Handle(CompletionNotification(SeedCompletedBooking(context, firstReferee.Id, 500m).Id), CancellationToken.None);

        var secondReferee = SeedCustomer(context, "SecondReferee");
        SeedRegisteredReferral(context, referrer, secondReferee, config);
        await BuildHandler(context).Handle(CompletionNotification(SeedCompletedBooking(context, secondReferee.Id, 500m).Id), CancellationToken.None);

        context.ReferralMilestoneAwards.Count(a => a.ReferralMilestoneId == milestone.Id).Should().Be(0);

        // Third referral brings the count to 3 - matches, bonus fires.
        var thirdReferee = SeedCustomer(context, "ThirdReferee");
        SeedRegisteredReferral(context, referrer, thirdReferee, config);
        await BuildHandler(context).Handle(CompletionNotification(SeedCompletedBooking(context, thirdReferee.Id, 500m).Id), CancellationToken.None);

        context.ReferralMilestoneAwards.Count(a => a.ReferralMilestoneId == milestone.Id && a.ReferrerCustomerId == referrer.Id).Should().Be(1);
    }

    [Fact]
    public async Task Referral_reward_disbursement_sends_a_ReferralRewardCredited_notification_that_actually_resolves_a_template()
    {
        using var context = _db.CreateContext();
        var config = SeedConfig(context);
        var referrer = SeedCustomer(context, "Referrer");
        var referee = SeedCustomer(context, "Referee");
        SeedRegisteredReferral(context, referrer, referee, config);
        var booking = SeedCompletedBooking(context, referee.Id, totalPayable: 500m);

        await BuildHandler(context).Handle(CompletionNotification(booking.Id), CancellationToken.None);

        // Task 172: before NotificationTemplateSeedData carried rows for
        // ReferralRewardCredited, this dispatch would have logged a
        // "no_template" Failed row instead - this pins down that the
        // template now resolves and the send genuinely succeeds.
        context.NotificationEvents
            .Where(e => e.EventType == NotificationEventType.ReferralRewardCredited && e.CustomerId == referrer.Id)
            .Should().NotBeEmpty()
            .And.OnlyContain(e => e.Status == NotificationDeliveryStatus.Sent);
    }
}
