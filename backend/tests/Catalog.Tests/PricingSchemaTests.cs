using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 47d/47e/47f/47g: city-wise pricing and per-city visit charge/tax/fee policy schema.</summary>
public sealed class PricingSchemaTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public PricingSchemaTests(TestDatabase db) => _db = db;

    [Fact]
    public void ServiceCityPrice_rejects_a_non_positive_price()
    {
        Action act = () => new ServiceCityPrice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetForServiceAndCityAsync_returns_the_override_when_configured()
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 499m);
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var override_ = new ServiceCityPrice(Guid.NewGuid(), service.Id, city.Id, 599m);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.States.Add(state);
            context.Cities.Add(city);
            context.ServiceCityPrices.Add(override_);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var repository = new ServiceCityPriceRepository(readContext);

        var loaded = await repository.GetForServiceAndCityAsync(service.Id, city.Id);

        loaded.Should().NotBeNull();
        loaded!.Price.Should().Be(599m);
    }

    [Theory]
    [InlineData(-1, 10, 5)]
    [InlineData(10, 101, 5)]
    [InlineData(10, 10, -5)]
    public void CityPricingPolicy_rejects_invalid_values(decimal visitCharge, decimal taxPercentage, decimal platformFee)
    {
        Action act = () => new CityPricingPolicy(Guid.NewGuid(), Guid.NewGuid(), visitCharge, taxPercentage, platformFee);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetByCityAsync_returns_the_configured_policy()
    {
        var state = new State(Guid.NewGuid(), "Delhi", "DL" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "New Delhi");
        var policy = new CityPricingPolicy(Guid.NewGuid(), city.Id, 99m, 18m, 15m);

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.CityPricingPolicies.Add(policy);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var repository = new CityPricingPolicyRepository(readContext);

        var loaded = await repository.GetByCityAsync(city.Id);

        loaded.Should().NotBeNull();
        loaded!.VisitCharge.Should().Be(99m);
        loaded.TaxPercentage.Should().Be(18m);
        loaded.PlatformFee.Should().Be(15m);
    }
}
