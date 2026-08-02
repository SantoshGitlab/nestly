using FluentAssertions;
using Nestly.Domain.NestlyCoins;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 203: the public GET /nestly-coins/program surface.</summary>
public sealed class NestlyCoinsCustomerServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public NestlyCoinsCustomerServiceTests(TestDatabase db) => _db = db;

    private static NestlyCoinsCustomerService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(new NestlyCoinsProgramConfigRepository(context));

    private static void ClearCustomerConfig(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        context.RemoveRange(context.Set<NestlyCoinsProgramConfig>().Where(c => c.Audience == NestlyCoinsAudience.Customer));
        context.SaveChanges();
    }

    [Fact]
    public async Task GetProgramAsync_returns_not_found_when_the_customer_program_has_never_been_configured()
    {
        using var context = _db.CreateContext();
        ClearCustomerConfig(context);

        var result = await BuildService(context).GetProgramAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NestlyCoinsProgram.NotActive");
    }

    [Fact]
    public async Task GetProgramAsync_returns_not_found_when_the_customer_program_is_configured_but_inactive()
    {
        using var context = _db.CreateContext();
        ClearCustomerConfig(context);
        context.Add(new NestlyCoinsProgramConfig(Guid.NewGuid(), NestlyCoinsAudience.Customer, 5m, 200m, true, null, 30, 3, isActive: false));
        context.SaveChanges();

        var result = await BuildService(context).GetProgramAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NestlyCoinsProgram.NotActive");
    }

    [Fact]
    public async Task GetProgramAsync_returns_only_the_public_fields_when_active()
    {
        using var context = _db.CreateContext();
        ClearCustomerConfig(context);
        context.Add(new NestlyCoinsProgramConfig(Guid.NewGuid(), NestlyCoinsAudience.Customer, 5m, 200m, true, 500m, 45, 7, isActive: true));
        context.SaveChanges();

        var result = await BuildService(context).GetProgramAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.EarnRatePer100.Should().Be(5m);
        result.Value.MinimumOrderAmount.Should().Be(200m);
        result.Value.RequireReorder.Should().BeTrue();
        result.Value.ExpiryDays.Should().Be(45);
    }
}
