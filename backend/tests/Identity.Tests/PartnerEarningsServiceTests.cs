using FluentAssertions;
using Nestly.Application.PartnerManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// The partner's own self-service earnings/payouts view (task 149c,
/// PARTNER.md API surface "Earnings"), wired to the real
/// <c>PartnerEarningLedgerEntry</c>/<c>PartnerPayout</c> entities (task 148)
/// rather than the earlier 501-stub EarningsController.
/// </summary>
public class PartnerEarningsServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly Guid _partnerId;
    private readonly Guid _otherPartnerId;

    public PartnerEarningsServiceTests()
    {
        using var context = _database.CreateContext();
        var partner = new Partner(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", PartnerType.Individual, "+919876543210");
        var otherPartner = new Partner(Guid.NewGuid(), "Meena Iyer", "Meena's Services", PartnerType.Individual, "+919876500000");
        _partnerId = partner.Id;
        _otherPartnerId = otherPartner.Id;
        context.AddRange(partner, otherPartner);
        context.SaveChanges();
    }

    private PartnerEarningsService CreateService(NestlyDbContext context) => new(
        new PartnerEarningLedgerService(new PartnerRepository(context), new PartnerEarningLedgerRepository(context)),
        new PartnerPayoutService(new PartnerRepository(context), new PartnerPayoutRepository(context), new PartnerEarningLedgerRepository(context)));

    private async Task CreditAsync(NestlyDbContext context, Guid partnerId, decimal amount)
    {
        var ledgerService = new PartnerEarningLedgerService(new PartnerRepository(context), new PartnerEarningLedgerRepository(context));
        var result = await ledgerService.RecordAdjustmentAsync(
            partnerId, new RecordPartnerEarningAdjustmentRequest(PartnerEarningEntryType.Credit, amount, PartnerEarningSourceType.JobCompletion, Guid.NewGuid(), "Job completed."));
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetSummaryAsync_reflects_the_partners_current_balance()
    {
        await using var context = _database.CreateContext();
        await CreditAsync(context, _partnerId, 500m);

        var result = await CreateService(context).GetSummaryAsync(_partnerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentBalance.Should().Be(500m);
    }

    [Fact]
    public async Task GetLedgerAsync_returns_the_partners_own_entries_only()
    {
        await using var context = _database.CreateContext();
        await CreditAsync(context, _partnerId, 300m);
        await CreditAsync(context, _otherPartnerId, 900m);

        var result = await CreateService(context).GetLedgerAsync(_partnerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Amount.Should().Be(300m);
    }

    [Fact]
    public async Task ListPayoutsAsync_scopes_the_search_to_the_caller()
    {
        await using var context = _database.CreateContext();
        await CreditAsync(context, _partnerId, 1000m);
        var payoutRepository = new PartnerPayoutRepository(context);
        var payoutService = new PartnerPayoutService(new PartnerRepository(context), payoutRepository, new PartnerEarningLedgerRepository(context));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        (await payoutService.CreateBatchAsync(_partnerId, new CreatePartnerPayoutRequest(today.AddDays(-7), today))).IsSuccess.Should().BeTrue();

        var result = await CreateService(context).ListPayoutsAsync(_partnerId, status: null, page: 1, pageSize: 20);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(p => p.PartnerId == _partnerId);
    }

    [Fact]
    public async Task GetPayoutDetailAsync_hides_a_payout_belonging_to_another_partner()
    {
        await using var context = _database.CreateContext();
        await CreditAsync(context, _otherPartnerId, 1000m);
        var payoutService = new PartnerPayoutService(new PartnerRepository(context), new PartnerPayoutRepository(context), new PartnerEarningLedgerRepository(context));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await payoutService.CreateBatchAsync(_otherPartnerId, new CreatePartnerPayoutRequest(today.AddDays(-7), today));
        created.IsSuccess.Should().BeTrue();

        var result = await CreateService(context).GetPayoutDetailAsync(_partnerId, created.Value.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PartnerPayout.NotFound");
    }

    [Fact]
    public async Task GetPayoutDetailAsync_returns_the_callers_own_payout()
    {
        await using var context = _database.CreateContext();
        await CreditAsync(context, _partnerId, 1000m);
        var payoutService = new PartnerPayoutService(new PartnerRepository(context), new PartnerPayoutRepository(context), new PartnerEarningLedgerRepository(context));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await payoutService.CreateBatchAsync(_partnerId, new CreatePartnerPayoutRequest(today.AddDays(-7), today));
        created.IsSuccess.Should().BeTrue();

        var result = await CreateService(context).GetPayoutDetailAsync(_partnerId, created.Value.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(1000m);
    }

    public void Dispose() => _database.Dispose();
}
