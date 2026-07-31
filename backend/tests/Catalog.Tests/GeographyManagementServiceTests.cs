using FluentAssertions;
using Nestly.Application.Geography;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 111: admin geography master CRUD (SRS 12.9.1).</summary>
public sealed class GeographyManagementServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public GeographyManagementServiceTests(TestDatabase db) => _db = db;

    private GeographyManagementService CreateService()
    {
        var context = _db.CreateContext();
        return new GeographyManagementService(
            new StateRepository(context),
            new CityRepository(context),
            new ZoneRepository(context),
            new LocalityRepository(context),
            new PincodeRepository(context));
    }

    [Fact]
    public async Task Creating_a_state_then_listing_returns_it()
    {
        var service = CreateService();

        var created = await service.CreateStateAsync(new StateCreateRequest("Punjab", "PB" + Guid.NewGuid().ToString("N")[..4]));

        created.IsSuccess.Should().BeTrue();
        var states = await service.ListStatesAsync();
        states.Should().Contain(s => s.Id == created.Value.Id && s.Name == "Punjab" && s.IsActive);
    }

    [Fact]
    public async Task Creating_a_state_with_a_duplicate_code_returns_conflict()
    {
        var service = CreateService();
        string code = "GJ" + Guid.NewGuid().ToString("N")[..4];
        (await service.CreateStateAsync(new StateCreateRequest("Gujarat", code))).IsSuccess.Should().BeTrue();

        var result = await service.CreateStateAsync(new StateCreateRequest("Gujarat Again", code));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geography.DuplicateStateCode");
    }

    [Fact]
    public async Task Renaming_and_deactivating_a_state_persists()
    {
        var service = CreateService();
        var state = (await service.CreateStateAsync(new StateCreateRequest("Kerala", "KL" + Guid.NewGuid().ToString("N")[..4]))).Value;

        var renamed = await service.UpdateStateAsync(state.Id, new StateUpdateRequest("Keralam"));
        renamed.IsSuccess.Should().BeTrue();
        renamed.Value.Name.Should().Be("Keralam");

        var deactivated = await service.SetStateActiveAsync(state.Id, false);
        deactivated.IsSuccess.Should().BeTrue();

        var states = await service.ListStatesAsync();
        states.Single(s => s.Id == state.Id).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Updating_an_unknown_state_returns_not_found()
    {
        var service = CreateService();

        var result = await service.UpdateStateAsync(Guid.NewGuid(), new StateUpdateRequest("Nowhere"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geography.StateNotFound");
    }

    [Fact]
    public async Task Creating_a_city_under_a_nonexistent_state_returns_not_found()
    {
        var service = CreateService();

        var result = await service.CreateCityAsync(new CityCreateRequest(Guid.NewGuid(), "Ghost City"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geography.StateNotFound");
    }

    [Fact]
    public async Task Creating_two_cities_with_the_same_name_in_a_state_returns_conflict()
    {
        var service = CreateService();
        var state = (await service.CreateStateAsync(new StateCreateRequest("Rajasthan", "RJ" + Guid.NewGuid().ToString("N")[..4]))).Value;
        (await service.CreateCityAsync(new CityCreateRequest(state.Id, "Jaipur"))).IsSuccess.Should().BeTrue();

        var result = await service.CreateCityAsync(new CityCreateRequest(state.Id, "Jaipur"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geography.DuplicateCityName");
    }

    [Fact]
    public async Task Full_hierarchy_can_be_created_and_listed_by_parent()
    {
        var service = CreateService();
        var state = (await service.CreateStateAsync(new StateCreateRequest("Karnataka", "KA" + Guid.NewGuid().ToString("N")[..4]))).Value;
        var city = (await service.CreateCityAsync(new CityCreateRequest(state.Id, "Bengaluru"))).Value;
        var zone = (await service.CreateZoneAsync(new ZoneCreateRequest(city.Id, "South Zone"))).Value;
        var pincode = (await service.CreatePincodeAsync(new PincodeCreateRequest(city.Id, "560" + Guid.NewGuid().ToString("N")[..3]))).Value;
        var locality = (await service.CreateLocalityAsync(new LocalityCreateRequest(zone.Id, pincode.Id, "Koramangala"))).Value;

        (await service.ListCitiesAsync(state.Id)).Should().Contain(c => c.Id == city.Id);
        (await service.ListZonesAsync(city.Id)).Should().Contain(z => z.Id == zone.Id);
        (await service.ListPincodesAsync(city.Id)).Should().Contain(p => p.Id == pincode.Id);
        (await service.ListLocalitiesAsync(zone.Id)).Should().Contain(l => l.Id == locality.Id && l.PincodeId == pincode.Id);
    }

    [Fact]
    public async Task Creating_a_pincode_with_a_duplicate_code_returns_conflict()
    {
        var service = CreateService();
        var state = (await service.CreateStateAsync(new StateCreateRequest("Tamil Nadu", "TN" + Guid.NewGuid().ToString("N")[..4]))).Value;
        var city = (await service.CreateCityAsync(new CityCreateRequest(state.Id, "Chennai"))).Value;
        string pincodeCode = "600" + Guid.NewGuid().ToString("N")[..3];
        (await service.CreatePincodeAsync(new PincodeCreateRequest(city.Id, pincodeCode))).IsSuccess.Should().BeTrue();

        var result = await service.CreatePincodeAsync(new PincodeCreateRequest(city.Id, pincodeCode));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Geography.DuplicatePincodeCode");
    }
}
