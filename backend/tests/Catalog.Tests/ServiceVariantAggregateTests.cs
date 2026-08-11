using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Covers the Phase 3 catalog redesign's ServiceVariant entity: construction, invariants, persistence.</summary>
public sealed class ServiceVariantAggregateTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceVariantAggregateTests(TestDatabase db) => _db = db;

    [Fact]
    public void New_variant_defaults_to_active_with_zero_sort_order()
    {
        var variant = new ServiceVariant(Guid.NewGuid(), Guid.NewGuid(), "Split AC", 499m, 90);

        variant.IsActive.Should().BeTrue();
        variant.SortOrder.Should().Be(0);
        variant.Name.Should().Be("Split AC");
        variant.Price.Should().Be(499m);
        variant.DurationMinutes.Should().Be(90);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void A_variant_cannot_be_created_with_a_non_positive_price(decimal price)
    {
        var act = () => new ServiceVariant(Guid.NewGuid(), Guid.NewGuid(), "Window AC", price, 60);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void A_variant_cannot_be_created_with_a_non_positive_duration(int durationMinutes)
    {
        var act = () => new ServiceVariant(Guid.NewGuid(), Guid.NewGuid(), "Window AC", 399m, durationMinutes);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Deactivating_a_variant_persists()
    {
        var category = new Category(Guid.NewGuid(), "Appliance Repair", "appliance-repair-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "AC Repair", "ac-repair-" + Guid.NewGuid(), "desc", 500m);
        var variant = new ServiceVariant(Guid.NewGuid(), service.Id, "Split AC", 599m, 90);
        variant.SetInclusionsOverride("Includes gas top-up");

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.Add(variant);
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            var toDeactivate = context.Set<ServiceVariant>().Single(v => v.Id == variant.Id);
            toDeactivate.Deactivate();
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var loaded = readContext.Set<ServiceVariant>().Single(v => v.Id == variant.Id);
        loaded.IsActive.Should().BeFalse();
        loaded.InclusionsOverride.Should().Be("Includes gas top-up");
    }
}
