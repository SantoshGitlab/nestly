using FluentAssertions;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.ProviderManagement;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Identity.Tests;

/// <summary>
/// The provider's own self-service earnings/payouts view (task 149c,
/// PROVIDER.md API surface "Earnings"), wired to the real
/// <c>ProviderEarningLedgerEntry</c>/<c>ProviderPayout</c> entities (task 148)
/// rather than the earlier 501-stub EarningsController.
/// </summary>
public class ProviderEarningsServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly Guid _providerId;
    private readonly Guid _otherProviderId;

    public ProviderEarningsServiceTests()
    {
        using var context = _database.CreateContext();
        var provider = new Provider(Guid.NewGuid(), "Ravi Kumar", "Ravi's Repairs", ProviderType.Individual, "+919876543210");
        var otherProvider = new Provider(Guid.NewGuid(), "Meena Iyer", "Meena's Services", ProviderType.Individual, "+919876500000");
        _providerId = provider.Id;
        _otherProviderId = otherProvider.Id;
        context.AddRange(provider, otherProvider);
        context.SaveChanges();
    }

    private ProviderEarningsService CreateService(NestlyDbContext context) => new(
        new ProviderEarningLedgerService(new ProviderRepository(context), new ProviderEarningLedgerRepository(context)),
        BuildPayoutService(context));

    private static ProviderPayoutService BuildPayoutService(NestlyDbContext context) => new(
        new ProviderRepository(context),
        new ProviderPayoutRepository(context),
        new ProviderEarningLedgerRepository(context),
        new AuditLogWriter(context, new StubAuditContextProvider()));

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, Guid.NewGuid(), IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }

    private async Task CreditAsync(NestlyDbContext context, Guid providerId, decimal amount)
    {
        var ledgerService = new ProviderEarningLedgerService(new ProviderRepository(context), new ProviderEarningLedgerRepository(context));
        var result = await ledgerService.RecordAdjustmentAsync(
            providerId, new RecordProviderEarningAdjustmentRequest(ProviderEarningEntryType.Credit, amount, ProviderEarningSourceType.JobCompletion, Guid.NewGuid(), "Job completed."));
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetSummaryAsync_reflects_the_providers_current_balance()
    {
        await using var context = _database.CreateContext();
        await CreditAsync(context, _providerId, 500m);

        var result = await CreateService(context).GetSummaryAsync(_providerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.CurrentBalance.Should().Be(500m);
    }

    [Fact]
    public async Task GetLedgerAsync_returns_the_providers_own_entries_only()
    {
        await using var context = _database.CreateContext();
        await CreditAsync(context, _providerId, 300m);
        await CreditAsync(context, _otherProviderId, 900m);

        var result = await CreateService(context).GetLedgerAsync(_providerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Amount.Should().Be(300m);
    }

    [Fact]
    public async Task ListPayoutsAsync_scopes_the_search_to_the_caller()
    {
        await using var context = _database.CreateContext();
        await CreditAsync(context, _providerId, 1000m);
        var payoutService = BuildPayoutService(context);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        (await payoutService.CreateBatchAsync(_providerId, new CreateProviderPayoutRequest(today.AddDays(-7), today))).IsSuccess.Should().BeTrue();

        var result = await CreateService(context).ListPayoutsAsync(_providerId, status: null, page: 1, pageSize: 20);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(p => p.ProviderId == _providerId);
    }

    [Fact]
    public async Task GetPayoutDetailAsync_hides_a_payout_belonging_to_another_provider()
    {
        await using var context = _database.CreateContext();
        await CreditAsync(context, _otherProviderId, 1000m);
        var payoutService = BuildPayoutService(context);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await payoutService.CreateBatchAsync(_otherProviderId, new CreateProviderPayoutRequest(today.AddDays(-7), today));
        created.IsSuccess.Should().BeTrue();

        var result = await CreateService(context).GetPayoutDetailAsync(_providerId, created.Value.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ProviderPayout.NotFound");
    }

    [Fact]
    public async Task GetPayoutDetailAsync_returns_the_callers_own_payout()
    {
        await using var context = _database.CreateContext();
        await CreditAsync(context, _providerId, 1000m);
        var payoutService = BuildPayoutService(context);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await payoutService.CreateBatchAsync(_providerId, new CreateProviderPayoutRequest(today.AddDays(-7), today));
        created.IsSuccess.Should().BeTrue();

        var result = await CreateService(context).GetPayoutDetailAsync(_providerId, created.Value.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(1000m);
    }

    /// <summary>
    /// Task 251/252: neither payout list endpoint validated its query string -
    /// admin PayoutsController.Search and provider EarningsController.ListPayouts
    /// both hand raw page/pageSize to this service. An unbounded pageSize let a
    /// single request materialize the whole payout table, and a page below 1
    /// reached PostgreSQL as a negative OFFSET (a hard error there, though
    /// in-memory SQLite tolerates it - hence asserting on the echoed values).
    /// </summary>
    [Theory]
    [InlineData(101)]
    [InlineData(10_000)]
    [InlineData(int.MaxValue)]
    public async Task ListPayoutsAsync_caps_an_oversized_page_size(int requestedPageSize)
    {
        await using var context = _database.CreateContext();

        var result = await CreateService(context).ListPayoutsAsync(_providerId, status: null, page: 1, pageSize: requestedPageSize);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task ListPayoutsAsync_normalizes_a_non_positive_page_to_the_first_page(int requestedPage)
    {
        await using var context = _database.CreateContext();

        var result = await CreateService(context).ListPayoutsAsync(_providerId, status: null, page: requestedPage, pageSize: 20);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(1);
    }

    [Fact]
    public async Task ListPayoutsAsync_substitutes_the_default_for_a_non_positive_page_size()
    {
        await using var context = _database.CreateContext();

        var result = await CreateService(context).ListPayoutsAsync(_providerId, status: null, page: 1, pageSize: 0);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task ListPayoutsAsync_survives_a_page_number_that_used_to_overflow_the_offset()
    {
        await using var context = _database.CreateContext();
        await CreditAsync(context, _providerId, 1000m);
        var payoutService = BuildPayoutService(context);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        (await payoutService.CreateBatchAsync(_providerId, new CreateProviderPayoutRequest(today.AddDays(-7), today))).IsSuccess.Should().BeTrue();

        // (page - 1) * pageSize wrapped negative here before task 261.
        var result = await CreateService(context).ListPayoutsAsync(_providerId, status: null, page: 2_000_000_000, pageSize: 100);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty("a page that far past the end is empty, not an error");
        result.Value.TotalCount.Should().Be(1);
    }

    public void Dispose() => _database.Dispose();
}
