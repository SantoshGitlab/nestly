using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 44c/44ca/44cb: holiday/blackout schema and range lookup.</summary>
public sealed class SlotBlackoutTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public SlotBlackoutTests(TestDatabase db) => _db = db;

    [Fact]
    public void Start_date_must_not_be_after_end_date()
    {
        Action act = () => new SlotBlackout(
            Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 5),
            SlotBlackoutType.Holiday, "Test");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CoversDate_is_inclusive_of_both_ends()
    {
        var blackout = new SlotBlackout(
            Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 9),
            SlotBlackoutType.Holiday, "Diwali");

        blackout.CoversDate(new DateOnly(2026, 11, 8)).Should().BeTrue();
        blackout.CoversDate(new DateOnly(2026, 11, 9)).Should().BeTrue();
        blackout.CoversDate(new DateOnly(2026, 11, 7)).Should().BeFalse();
        blackout.CoversDate(new DateOnly(2026, 11, 10)).Should().BeFalse();
    }

    [Fact]
    public async Task ListInRangeAsync_returns_blackouts_overlapping_the_requested_range()
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var overlapping = new SlotBlackout(Guid.NewGuid(), city.Id, new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 9), SlotBlackoutType.Holiday, "Diwali");
        var outsideRange = new SlotBlackout(Guid.NewGuid(), city.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), SlotBlackoutType.Holiday, "New Year");

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.SlotBlackouts.AddRange(overlapping, outsideRange);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var repository = new SlotBlackoutRepository(readContext);

        var result = await repository.ListInRangeAsync(city.Id, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        result.Should().ContainSingle(b => b.Id == overlapping.Id);
    }
}
