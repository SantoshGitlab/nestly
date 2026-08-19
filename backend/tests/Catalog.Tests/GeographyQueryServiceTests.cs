using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers the public geography API backing location selection (SRS 11.1, 11.4.1).</summary>
public sealed class GeographyQueryServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public GeographyQueryServiceTests(TestDatabase db) => _db = db;

    private GeographyQueryService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) =>
        new(new GeographyRepository(context));

    [Fact]
    public async Task Lists_active_cities_with_their_state_name_ordered_alphabetically()
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var bengaluru = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var mysuru = new City(Guid.NewGuid(), state.Id, "Mysuru");
        var inactiveCity = new City(Guid.NewGuid(), state.Id, "Zzz Inactive");
        inactiveCity.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.AddRange(bengaluru, mysuru, inactiveCity);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var cities = await BuildService(readContext).ListActiveCitiesAsync();

        cities.Should().Contain(c => c.Id == bengaluru.Id && c.StateName == "Karnataka");
        cities.Should().Contain(c => c.Id == mysuru.Id);
        cities.Should().NotContain(c => c.Id == inactiveCity.Id);
        var indexOfBengaluru = cities.ToList().FindIndex(c => c.Id == bengaluru.Id);
        var indexOfMysuru = cities.ToList().FindIndex(c => c.Id == mysuru.Id);
        indexOfBengaluru.Should().BeLessThan(indexOfMysuru);
    }

    [Fact]
    public async Task Unknown_city_returns_not_found_when_searching_localities()
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).SearchLocalitiesAsync(Guid.NewGuid(), null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geography.CityNotFound");
    }

    [Fact]
    public async Task Searching_localities_filters_by_name_or_pincode_and_excludes_other_cities()
    {
        var state = new State(Guid.NewGuid(), "Delhi", "DL" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "New Delhi");
        var otherCity = new City(Guid.NewGuid(), state.Id, "Gurugram");
        var zone = new Zone(Guid.NewGuid(), city.Id, "Central");
        var otherZone = new Zone(Guid.NewGuid(), otherCity.Id, "South");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, "110001");
        var otherPincode = new Pincode(Guid.NewGuid(), otherCity.Id, "122001");
        var connaughtPlace = new Locality(Guid.NewGuid(), zone.Id, pincode.Id, "Connaught Place");
        var karolBagh = new Locality(Guid.NewGuid(), zone.Id, pincode.Id, "Karol Bagh");
        var elsewhere = new Locality(Guid.NewGuid(), otherZone.Id, otherPincode.Id, "Cyber City");

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.AddRange(city, otherCity);
            context.Zones.AddRange(zone, otherZone);
            context.Pincodes.AddRange(pincode, otherPincode);
            context.Localities.AddRange(connaughtPlace, karolBagh, elsewhere);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var byName = await BuildService(readContext).SearchLocalitiesAsync(city.Id, "connaught");
        byName.Value.Should().ContainSingle(l => l.Id == connaughtPlace.Id);

        using var readContext2 = _db.CreateContext();
        var byPincode = await BuildService(readContext2).SearchLocalitiesAsync(city.Id, "110001");
        byPincode.Value.Should().Contain(l => l.Id == connaughtPlace.Id).And.Contain(l => l.Id == karolBagh.Id);
        byPincode.Value.Should().NotContain(l => l.Id == elsewhere.Id);
    }

    [Fact]
    public async Task Resolves_a_pincode_to_its_city_and_state_for_address_autofill()
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, "560034");

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.Pincodes.Add(pincode);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).ResolvePincodeAsync("560034");

        result.IsSuccess.Should().BeTrue();
        result.Value.CityId.Should().Be(city.Id);
        result.Value.CityName.Should().Be("Bengaluru");
        result.Value.StateName.Should().Be("Karnataka");
    }

    [Fact]
    public async Task Unknown_pincode_returns_not_found()
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).ResolvePincodeAsync("999999");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geography.PincodeNotFound");
    }

    [Fact]
    public async Task Inactive_pincode_is_not_resolved()
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, "560001");
        pincode.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.Pincodes.Add(pincode);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).ResolvePincodeAsync("560001");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geography.PincodeNotFound");
    }
}
