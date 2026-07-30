using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 40c: Service aggregate inclusions/exclusions fields.</summary>
public sealed class ServiceAggregateTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceAggregateTests(TestDatabase db) => _db = db;

    [Fact]
    public void Inclusions_and_exclusions_round_trip_through_the_database()
    {
        var category = new Category(Guid.NewGuid(), "Appliance Repair", "appliance-repair-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "AC Service", "Deep clean", 499m);
        service.SetInclusions("Filter cleaning, gas top-up check");
        service.SetExclusions("Gas refill, part replacement");

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var loaded = readContext.Set<Service>().Single(s => s.Id == service.Id);
        loaded.Inclusions.Should().Be("Filter cleaning, gas top-up check");
        loaded.Exclusions.Should().Be("Gas refill, part replacement");
    }
}
