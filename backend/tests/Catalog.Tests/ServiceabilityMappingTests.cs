using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 39: serviceability mapping schema - category-city and
/// service-pincode mappings, including the deactivation toggle used for
/// temporary suspension / blackout in a given area.
/// </summary>
public sealed class ServiceabilityMappingTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceabilityMappingTests(TestDatabase db) => _db = db;

    [Fact]
    public void Category_city_mapping_can_be_deactivated_to_suspend_serviceability()
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning", "Home cleaning services");
        var state = new State(Guid.NewGuid(), "Karnataka", "KA");
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var mapping = new CategoryCityMapping(Guid.NewGuid(), category.Id, city.Id);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.States.Add(state);
            context.Cities.Add(city);
            context.CategoryCityMappings.Add(mapping);
            context.SaveChanges();
        }

        using (var context = _db.CreateContext())
        {
            var loaded = context.CategoryCityMappings.Single(m => m.Id == mapping.Id);
            loaded.IsActive.Should().BeTrue();

            loaded.Deactivate();
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        readContext.CategoryCityMappings.Single(m => m.Id == mapping.Id).IsActive.Should().BeFalse();
    }

    [Fact]
    public void Mapping_the_same_category_to_the_same_city_twice_violates_the_unique_index()
    {
        var category = new Category(Guid.NewGuid(), "Plumbing", "plumbing", "Plumbing services");
        var state = new State(Guid.NewGuid(), "Maharashtra", "MH");
        var city = new City(Guid.NewGuid(), state.Id, "Pune");

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.States.Add(state);
            context.Cities.Add(city);
            context.CategoryCityMappings.Add(new CategoryCityMapping(Guid.NewGuid(), category.Id, city.Id));
            context.SaveChanges();
        }

        using var context2 = _db.CreateContext();
        context2.CategoryCityMappings.Add(new CategoryCityMapping(Guid.NewGuid(), category.Id, city.Id));

        var act = () => context2.SaveChanges();

        act.Should().Throw<DbUpdateException>().WithInnerException<SqliteException>();
    }

    [Fact]
    public void Service_pincode_mapping_with_a_nonexistent_service_violates_the_foreign_key_constraint()
    {
        var state = new State(Guid.NewGuid(), "Delhi", "DL");
        var city = new City(Guid.NewGuid(), state.Id, "New Delhi");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, "110002");

        using var context = _db.CreateContext();
        context.States.Add(state);
        context.Cities.Add(city);
        context.Pincodes.Add(pincode);
        context.ServicePincodeMappings.Add(new ServicePincodeMapping(Guid.NewGuid(), Guid.NewGuid(), pincode.Id));

        var act = () => context.SaveChanges();

        act.Should().Throw<DbUpdateException>().WithInnerException<SqliteException>();
    }
}
