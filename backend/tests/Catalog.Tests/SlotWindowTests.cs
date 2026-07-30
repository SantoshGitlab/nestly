using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 44a/44b/44f: slot windows, day-of-week rules, capacity.</summary>
public sealed class SlotWindowTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public SlotWindowTests(TestDatabase db) => _db = db;

    [Fact]
    public void Start_time_must_be_before_end_time()
    {
        Action act = () => new SlotWindow(Guid.NewGuid(), Guid.NewGuid(), "Morning", TimeSpan.FromHours(13), TimeSpan.FromHours(9));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Capacity_must_be_positive_when_set()
    {
        var window = new SlotWindow(Guid.NewGuid(), Guid.NewGuid(), "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));

        Action act = () => window.SetCapacity(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Window_with_day_of_week_rules_persists_and_reloads_correctly()
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var window = new SlotWindow(Guid.NewGuid(), city.Id, "Morning", TimeSpan.FromHours(9), TimeSpan.FromHours(13));
        window.SetCapacity(10);
        var mondayRule = new SlotWindowRule(Guid.NewGuid(), window.Id, DayOfWeek.Monday);
        var fridayRule = new SlotWindowRule(Guid.NewGuid(), window.Id, DayOfWeek.Friday);

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.SlotWindows.Add(window);
            context.SlotWindowRules.AddRange(mondayRule, fridayRule);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var loaded = readContext.SlotWindows.Single(w => w.Id == window.Id);
        loaded.MaxBookingsPerSlot.Should().Be(10);
        readContext.SlotWindowRules.Count(r => r.SlotWindowId == window.Id).Should().Be(2);
    }

    [Fact]
    public void Deleting_a_window_cascades_to_its_day_of_week_rules()
    {
        var state = new State(Guid.NewGuid(), "Delhi", "DL" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "New Delhi");
        var window = new SlotWindow(Guid.NewGuid(), city.Id, "Evening", TimeSpan.FromHours(17), TimeSpan.FromHours(21));
        var rule = new SlotWindowRule(Guid.NewGuid(), window.Id, DayOfWeek.Saturday);

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.SlotWindows.Add(window);
            context.SlotWindowRules.Add(rule);
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            context.SlotWindows.Remove(context.SlotWindows.Single(w => w.Id == window.Id));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        readContext.SlotWindowRules.Any(r => r.SlotWindowId == window.Id).Should().BeFalse();
    }
}
