using FluentAssertions;
using Nestly.Application;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 74a-c: append-only ledger, source-event reference, and the balance API.</summary>
public sealed class WalletServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public WalletServiceTests(TestDatabase db) => _db = db;

    private static WalletService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(new WalletLedgerRepository(context));

    private static Guid SeedCustomer(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Wallet Customer", CustomerStatus.Active);
        context.Add(customer);
        context.SaveChanges();
        return customer.Id;
    }

    [Fact]
    public async Task GetBalanceAsync_returns_zero_for_a_customer_with_no_activity()
    {
        using var context = _db.CreateContext();
        var customerId = SeedCustomer(context);
        var result = await BuildService(context).GetBalanceAsync(customerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Balance.Should().Be(0m);
    }

    [Fact]
    public async Task CreditAsync_increases_the_balance_and_references_its_source_event()
    {
        Guid customerId;
        var refundId = Guid.NewGuid();

        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            var entry = await BuildService(context).CreditAsync(
                customerId, 250m, WalletSourceType.Refund, refundId, "Refund for cancelled booking");

            entry.EntryType.Should().Be(WalletEntryType.Credit);
            entry.BalanceAfter.Should().Be(250m);
            entry.SourceReferenceId.Should().Be(refundId);
        }

        using var readContext = _db.CreateContext();
        var balance = await BuildService(readContext).GetBalanceAsync(customerId);
        balance.Value.Balance.Should().Be(250m);
    }

    [Fact]
    public async Task Successive_credits_and_debits_accumulate_a_correct_running_balance()
    {
        Guid customerId;

        using (var c1 = _db.CreateContext())
        {
            customerId = SeedCustomer(c1);
            await BuildService(c1).CreditAsync(customerId, 500m, WalletSourceType.Refund, Guid.NewGuid(), "Refund 1");
        }

        using (var c2 = _db.CreateContext())
        {
            var debit = await BuildService(c2).DebitAsync(customerId, 200m, WalletSourceType.ManualAdjustment, null, "Adjustment");
            debit.IsSuccess.Should().BeTrue();
            debit.Value.BalanceAfter.Should().Be(300m);
        }

        using (var c3 = _db.CreateContext())
        {
            await BuildService(c3).CreditAsync(customerId, 100m, WalletSourceType.PromotionalCredit, null, "Promo");
        }

        using var readContext = _db.CreateContext();
        var balance = await BuildService(readContext).GetBalanceAsync(customerId);
        balance.Value.Balance.Should().Be(400m);

        var ledger = await BuildService(readContext).GetLedgerAsync(customerId);
        ledger.Value.Should().HaveCount(3);
        ledger.Value.Should().BeInDescendingOrder(e => e.CreatedAtUtc);
    }

    [Fact]
    public async Task DebitAsync_rejects_a_debit_that_would_take_the_balance_negative()
    {
        Guid customerId;

        using (var context = _db.CreateContext())
        {
            customerId = SeedCustomer(context);
            await BuildService(context).CreditAsync(customerId, 100m, WalletSourceType.Refund, Guid.NewGuid(), "Refund");
        }

        using var debitContext = _db.CreateContext();
        var result = await BuildService(debitContext).DebitAsync(customerId, 150m, WalletSourceType.ManualAdjustment, null, "Over-debit");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Wallet.InsufficientBalance");

        using var readContext = _db.CreateContext();
        var balance = await BuildService(readContext).GetBalanceAsync(customerId);
        balance.Value.Balance.Should().Be(100m, "a rejected debit must not be recorded");
    }

    [Fact]
    public async Task GetLedgerAsync_never_mixes_two_different_customers_entries()
    {
        Guid customerA, customerB;

        using (var context = _db.CreateContext())
        {
            customerA = SeedCustomer(context);
            customerB = SeedCustomer(context);
            var service = BuildService(context);
            await service.CreditAsync(customerA, 100m, WalletSourceType.Refund, Guid.NewGuid(), "A's refund");
            await service.CreditAsync(customerB, 500m, WalletSourceType.Refund, Guid.NewGuid(), "B's refund");
        }

        using var readContext = _db.CreateContext();
        var service2 = BuildService(readContext);
        var ledgerA = await service2.GetLedgerAsync(customerA);
        ledgerA.Value.Should().ContainSingle();
        ledgerA.Value[0].Amount.Should().Be(100m);

        var balanceB = await service2.GetBalanceAsync(customerB);
        balanceB.Value.Balance.Should().Be(500m);
    }
}
