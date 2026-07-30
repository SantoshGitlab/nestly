using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 44d/44e: cutoff and advance-booking rules.</summary>
public sealed class SlotBookingPolicyTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public SlotBookingPolicyTests(TestDatabase db) => _db = db;

    [Fact]
    public void Cutoff_minutes_cannot_be_negative()
    {
        Action act = () => new SlotBookingPolicy(Guid.NewGuid(), Guid.NewGuid(), -1, 7);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Max_advance_days_must_be_positive()
    {
        Action act = () => new SlotBookingPolicy(Guid.NewGuid(), Guid.NewGuid(), 60, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Only_one_policy_per_city_is_allowed()
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.SlotBookingPolicies.Add(new SlotBookingPolicy(Guid.NewGuid(), city.Id, 60, 7));
            context.SaveChanges();
        }

        using var context2 = _db.CreateContext();
        context2.SlotBookingPolicies.Add(new SlotBookingPolicy(Guid.NewGuid(), city.Id, 30, 14));

        Action act = () => context2.SaveChanges();

        act.Should().Throw<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    [Fact]
    public async Task GetByCityAsync_returns_the_configured_policy()
    {
        var state = new State(Guid.NewGuid(), "Delhi", "DL" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "New Delhi");
        var policy = new SlotBookingPolicy(Guid.NewGuid(), city.Id, 90, 10);

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.SlotBookingPolicies.Add(policy);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var repository = new SlotBookingPolicyRepository(readContext);

        var loaded = await repository.GetByCityAsync(city.Id);

        loaded.Should().NotBeNull();
        loaded!.CutoffMinutes.Should().Be(90);
        loaded.MaxAdvanceDays.Should().Be(10);
    }
}
