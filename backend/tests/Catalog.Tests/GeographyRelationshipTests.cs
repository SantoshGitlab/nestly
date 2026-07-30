using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 38bd: verifies the geography navigation properties and the
/// Fluent API foreign key / unique index configuration added for
/// State -> City -> {Zone, Pincode} -> Locality actually hold, using real
/// sample data rather than mocks.
/// </summary>
public sealed class GeographyRelationshipTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public GeographyRelationshipTests(TestDatabase db) => _db = db;

    [Fact]
    public void Saving_full_geography_hierarchy_and_querying_through_navigation_properties_returns_linked_data()
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA");
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var zone = new Zone(Guid.NewGuid(), city.Id, "South Zone");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, "560001");
        var locality = new Locality(Guid.NewGuid(), zone.Id, pincode.Id, "Koramangala");

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.Zones.Add(zone);
            context.Pincodes.Add(pincode);
            context.Localities.Add(locality);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var loadedLocality = readContext.Localities
            .Include(l => l.Zone).ThenInclude(z => z!.City).ThenInclude(c => c!.State)
            .Include(l => l.Pincode)
            .Single(l => l.Id == locality.Id);

        loadedLocality.Zone.Should().NotBeNull();
        loadedLocality.Zone!.Name.Should().Be("South Zone");
        loadedLocality.Zone.City.Should().NotBeNull();
        loadedLocality.Zone.City!.Name.Should().Be("Bengaluru");
        loadedLocality.Zone.City.State.Should().NotBeNull();
        loadedLocality.Zone.City.State!.Code.Should().Be("KA");

        loadedLocality.Pincode.Should().NotBeNull();
        loadedLocality.Pincode!.Code.Should().Be("560001");
        loadedLocality.Pincode.CityId.Should().Be(city.Id);
    }

    [Fact]
    public void Inserting_a_city_with_a_nonexistent_state_violates_the_foreign_key_constraint()
    {
        using var context = _db.CreateContext();
        var orphanCity = new City(Guid.NewGuid(), Guid.NewGuid(), "Orphan City");
        context.Cities.Add(orphanCity);

        var act = () => context.SaveChanges();

        act.Should().Throw<DbUpdateException>()
            .WithInnerException<SqliteException>();
    }

    [Fact]
    public void Two_cities_with_the_same_name_in_the_same_state_violate_the_unique_index()
    {
        var state = new State(Guid.NewGuid(), "Maharashtra", "MH");

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(new City(Guid.NewGuid(), state.Id, "Pune"));
            context.SaveChanges();
        }

        using var context2 = _db.CreateContext();
        context2.Cities.Add(new City(Guid.NewGuid(), state.Id, "Pune"));

        var act = () => context2.SaveChanges();

        act.Should().Throw<DbUpdateException>()
            .WithInnerException<SqliteException>();
    }

    [Fact]
    public void Two_pincodes_with_the_same_code_violate_the_global_unique_index()
    {
        var state = new State(Guid.NewGuid(), "Delhi", "DL");
        var cityOne = new City(Guid.NewGuid(), state.Id, "New Delhi");
        var cityTwo = new City(Guid.NewGuid(), state.Id, "Old Delhi");

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.AddRange(cityOne, cityTwo);
            context.Pincodes.Add(new Pincode(Guid.NewGuid(), cityOne.Id, "110001"));
            context.SaveChanges();
        }

        using var context2 = _db.CreateContext();
        context2.Pincodes.Add(new Pincode(Guid.NewGuid(), cityTwo.Id, "110001"));

        var act = () => context2.SaveChanges();

        act.Should().Throw<DbUpdateException>()
            .WithInnerException<SqliteException>();
    }
}
