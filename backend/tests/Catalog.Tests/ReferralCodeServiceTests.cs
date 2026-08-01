using FluentAssertions;
using Microsoft.Extensions.Options;
using Nestly.Application;
using Nestly.Domain;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 162: referral code generation, uniqueness, lazy set-once assignment, and the share link.</summary>
public sealed class ReferralCodeServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ReferralCodeServiceTests(TestDatabase db) => _db = db;

    private static ReferralCodeService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context, ReferralOptions? options = null) =>
        new(new CustomerRepository(context), Options.Create(options ?? new ReferralOptions()));

    private static Guid SeedCustomer(Nestly.Infrastructure.Persistence.NestlyDbContext context)
    {
        var customer = new Customer(Guid.NewGuid(), "9" + Guid.NewGuid().ToString("N")[..9], "Referral Customer", CustomerStatus.Active);
        context.Add(customer);
        context.SaveChanges();
        return customer.Id;
    }

    [Fact]
    public async Task GetOrCreateCodeAsync_generates_and_persists_a_code_on_first_call()
    {
        using var context = _db.CreateContext();
        var customerId = SeedCustomer(context);

        string code = await BuildService(context).GetOrCreateCodeAsync(customerId);

        code.Should().HaveLength(8);
        context.Set<Customer>().Single(c => c.Id == customerId).ReferralCode.Should().Be(code);
    }

    [Fact]
    public async Task GetOrCreateCodeAsync_returns_the_same_code_on_a_second_call()
    {
        using var context = _db.CreateContext();
        var customerId = SeedCustomer(context);
        var service = BuildService(context);

        string first = await service.GetOrCreateCodeAsync(customerId);
        string second = await service.GetOrCreateCodeAsync(customerId);

        second.Should().Be(first);
    }

    [Fact]
    public async Task GetOrCreateCodeAsync_never_collides_across_customers()
    {
        using var context = _db.CreateContext();
        var service = BuildService(context);
        var codes = new HashSet<string>();

        for (int i = 0; i < 25; i++)
        {
            var customerId = SeedCustomer(context);
            codes.Add(await service.GetOrCreateCodeAsync(customerId));
        }

        codes.Should().HaveCount(25);
    }

    [Fact]
    public void BuildShareLink_appends_the_code_to_the_configured_base_url()
    {
        using var context = _db.CreateContext();
        var service = BuildService(context, new ReferralOptions { ShareLinkBaseUrl = "https://example.test/register?ref=" });

        service.BuildShareLink("ABCD1234").Should().Be("https://example.test/register?ref=ABCD1234");
    }
}
