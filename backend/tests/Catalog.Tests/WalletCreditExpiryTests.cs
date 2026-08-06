using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Nestly.Application;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 175: FIFO consumption of expiring wallet credit (soonest-to-expire
/// drawn down first) and the scheduled sweep that writes off whatever is left
/// unconsumed once a credit's expiry passes.
/// </summary>
public sealed class WalletCreditExpiryTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public WalletCreditExpiryTests(TestDatabase db) => _db = db;

    private static WalletService BuildWalletService(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(new WalletLedgerRepository(context), context);

    private static WalletCreditExpirySweepJob BuildSweepJob(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(new WalletLedgerRepository(context), BuildWalletService(context), NullLogger<WalletCreditExpirySweepJob>.Instance);

    private static Guid SeedCustomer(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Expiry Customer", CustomerStatus.Active);
        context.Add(customer);
        context.SaveChanges();
        return customer.Id;
    }

    [Fact]
    public async Task DebitAsync_consumes_the_soonest_to_expire_credit_first()
    {
        Guid customerId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            var service = BuildWalletService(context);

            // Credited out of chronological order deliberately - consumption
            // order must be driven by ExpiresAtUtc, not insertion order.
            await service.CreditAsync(customerId, 100m, WalletSourceType.ReferralReward, Guid.NewGuid(), "Later-expiring", DateTime.UtcNow.AddDays(10));
            await service.CreditAsync(customerId, 50m, WalletSourceType.ReferralReward, Guid.NewGuid(), "Sooner-expiring", DateTime.UtcNow.AddDays(5));
        }

        using (var context = _db.CreateContext())
        {
            var debit = await BuildWalletService(context).DebitAsync(customerId, 30m, WalletSourceType.ManualAdjustment, null, "Spend");
            debit.IsSuccess.Should().BeTrue();
        }

        using var readContext = _db.CreateContext();
        var credits = readContext.WalletLedgerEntries
            .Where(e => e.CustomerId == customerId && e.EntryType == WalletEntryType.Credit)
            .OrderBy(e => e.ExpiresAtUtc)
            .ToList();

        credits.Single(e => e.Description == "Sooner-expiring").RemainingAmount.Should().Be(20m, "the 30 debit should draw entirely from the sooner-expiring 50 credit first");
        credits.Single(e => e.Description == "Later-expiring").RemainingAmount.Should().Be(100m, "the later-expiring credit must be untouched while the sooner one still has enough remaining");
    }

    [Fact]
    public async Task DebitAsync_spills_over_into_the_next_soonest_credit_once_the_first_is_exhausted()
    {
        Guid customerId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            var service = BuildWalletService(context);
            await service.CreditAsync(customerId, 100m, WalletSourceType.ReferralReward, Guid.NewGuid(), "Later", DateTime.UtcNow.AddDays(10));
            await service.CreditAsync(customerId, 50m, WalletSourceType.ReferralReward, Guid.NewGuid(), "Sooner", DateTime.UtcNow.AddDays(5));
        }

        using (var context = _db.CreateContext())
        {
            // Bigger than the sooner-expiring credit alone (50) - must spill
            // 30 of it over into the later-expiring credit.
            var debit = await BuildWalletService(context).DebitAsync(customerId, 80m, WalletSourceType.ManualAdjustment, null, "Spend");
            debit.IsSuccess.Should().BeTrue();
        }

        using var readContext = _db.CreateContext();
        var credits = readContext.WalletLedgerEntries
            .Where(e => e.CustomerId == customerId && e.EntryType == WalletEntryType.Credit)
            .ToList();

        credits.Single(e => e.Description == "Sooner").RemainingAmount.Should().Be(0m);
        credits.Single(e => e.Description == "Later").RemainingAmount.Should().Be(70m);
    }

    [Fact]
    public async Task SweepAsync_writes_off_the_unconsumed_portion_of_an_expired_credit()
    {
        Guid customerId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            // Already expired at creation time - the sweep's job is to find
            // exactly this kind of already-past-due, still-unspent credit.
            await BuildWalletService(context).CreditAsync(
                customerId, 200m, WalletSourceType.ReferralReward, Guid.NewGuid(), "Expired credit", DateTime.UtcNow.AddDays(-1));
        }

        using (var context = _db.CreateContext())
        {
            await BuildSweepJob(context).SweepAsync();
        }

        using var readContext = _db.CreateContext();
        var balance = await BuildWalletService(readContext).GetBalanceAsync(customerId);
        balance.Value.Balance.Should().Be(0m, "the whole unspent expired credit must be written off");

        var writeOff = readContext.WalletLedgerEntries.Single(e => e.CustomerId == customerId && e.SourceType == WalletSourceType.ReferralCreditExpiry);
        writeOff.Amount.Should().Be(200m);
        writeOff.EntryType.Should().Be(WalletEntryType.Debit);

        var expiredCredit = readContext.WalletLedgerEntries.Single(e => e.CustomerId == customerId && e.Description == "Expired credit");
        expiredCredit.RemainingAmount.Should().Be(0m);
    }

    [Fact]
    public async Task SweepAsync_only_writes_off_the_still_unconsumed_remainder_not_the_full_original_amount()
    {
        Guid customerId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            var service = BuildWalletService(context);
            await service.CreditAsync(customerId, 200m, WalletSourceType.ReferralReward, Guid.NewGuid(), "Expired credit", DateTime.UtcNow.AddDays(-1));
        }

        using (var context = _db.CreateContext())
        {
            // Spend 120 of it before it's swept - only the remaining 80
            // should ever be written off.
            await BuildWalletService(context).DebitAsync(customerId, 120m, WalletSourceType.ManualAdjustment, null, "Partial spend");
        }

        using (var context = _db.CreateContext())
        {
            await BuildSweepJob(context).SweepAsync();
        }

        using var readContext = _db.CreateContext();
        var balance = await BuildWalletService(readContext).GetBalanceAsync(customerId);
        balance.Value.Balance.Should().Be(0m);

        var writeOff = readContext.WalletLedgerEntries.Single(e => e.CustomerId == customerId && e.SourceType == WalletSourceType.ReferralCreditExpiry);
        writeOff.Amount.Should().Be(80m, "only the unconsumed remainder, not the original 200, should be written off");
    }

    [Fact]
    public async Task SweepAsync_is_idempotent_and_never_writes_off_the_same_credit_twice()
    {
        Guid customerId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            await BuildWalletService(context).CreditAsync(
                customerId, 200m, WalletSourceType.ReferralReward, Guid.NewGuid(), "Expired credit", DateTime.UtcNow.AddDays(-1));
        }

        using (var context = _db.CreateContext())
        {
            await BuildSweepJob(context).SweepAsync();
        }

        // Re-run - simulates a Hangfire retry re-executing the whole job.
        using (var context = _db.CreateContext())
        {
            await BuildSweepJob(context).SweepAsync();
        }

        using var readContext = _db.CreateContext();
        readContext.WalletLedgerEntries.Count(e => e.CustomerId == customerId && e.SourceType == WalletSourceType.ReferralCreditExpiry).Should().Be(1, "a retried sweep must not write off the same already-swept credit a second time");

        var balance = await BuildWalletService(readContext).GetBalanceAsync(customerId);
        balance.Value.Balance.Should().Be(0m, "balance must not go negative from a duplicate write-off");
    }

    [Fact]
    public async Task DebitAsync_ignores_expiring_credits_that_have_already_expired()
    {
        Guid customerId;
        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            var service = BuildWalletService(context);
            // Already-expired credit still sitting unswept, plus a healthy
            // non-expiring credit.
            await service.CreditAsync(customerId, 50m, WalletSourceType.ReferralReward, Guid.NewGuid(), "Expired", DateTime.UtcNow.AddDays(-1));
            await service.CreditAsync(customerId, 100m, WalletSourceType.Refund, Guid.NewGuid(), "Non-expiring");
        }

        using (var context = _db.CreateContext())
        {
            var debit = await BuildWalletService(context).DebitAsync(customerId, 30m, WalletSourceType.ManualAdjustment, null, "Spend");
            debit.IsSuccess.Should().BeTrue();
        }

        using var readContext = _db.CreateContext();
        readContext.WalletLedgerEntries.Single(e => e.Description == "Expired").RemainingAmount.Should().Be(50m, "an already-expired credit must not be drawn against by a fresh debit - only the sweep may touch it");
    }
}
