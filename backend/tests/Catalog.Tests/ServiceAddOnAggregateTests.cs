using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 40d: AddOn aggregate.</summary>
public sealed class ServiceAddOnAggregateTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceAddOnAggregateTests(TestDatabase db) => _db = db;

    [Fact]
    public void New_addon_defaults_to_active_and_persists_description_and_sort_order()
    {
        var category = new Category(Guid.NewGuid(), "Salon", "salon-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Haircut", "haircut-" + Guid.NewGuid(), "desc", 299m);
        var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "Head Massage", 99m);
        addOn.SetDescription("10-minute relaxing head massage");
        addOn.SetSortOrder(1);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.Add(addOn);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var loaded = readContext.Set<ServiceAddOn>().Single(a => a.Id == addOn.Id);
        loaded.IsActive.Should().BeTrue();
        loaded.Description.Should().Be("10-minute relaxing head massage");
        loaded.SortOrder.Should().Be(1);
    }

    [Fact]
    public void Deactivating_an_addon_persists()
    {
        var category = new Category(Guid.NewGuid(), "Salon2", "salon-2-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Facial", "facial-" + Guid.NewGuid(), "desc", 799m);
        var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "Extra Steam", 49m);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.Add(addOn);
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            var toDeactivate = context.Set<ServiceAddOn>().Single(a => a.Id == addOn.Id);
            toDeactivate.Deactivate();
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        readContext.Set<ServiceAddOn>().Single(a => a.Id == addOn.Id).IsActive.Should().BeFalse();
    }
}
