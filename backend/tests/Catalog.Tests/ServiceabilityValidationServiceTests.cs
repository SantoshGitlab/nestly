using FluentAssertions;
using Nestly.Application.Serviceability;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 43: serviceability validation service.</summary>
public sealed class ServiceabilityValidationServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceabilityValidationServiceTests(TestDatabase db) => _db = db;

    [Fact]
    public async Task Category_active_in_city_is_reported_serviceable()
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.States.Add(state);
            context.Cities.Add(city);
            context.CategoryCityMappings.Add(new CategoryCityMapping(Guid.NewGuid(), category.Id, city.Id));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(readContext));

        var result = await service.IsCategoryServiceableAsync(category.Id, city.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Category_with_no_mapping_in_city_is_reported_not_serviceable()
    {
        var category = new Category(Guid.NewGuid(), "Painting", "painting-" + Guid.NewGuid(), "desc");
        var state = new State(Guid.NewGuid(), "Maharashtra", "MH" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Pune");

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.States.Add(state);
            context.Cities.Add(city);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(readContext));

        var result = await service.IsCategoryServiceableAsync(category.Id, city.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_city_returns_a_not_found_error()
    {
        using var context = _db.CreateContext();
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(context));

        var result = await service.IsCategoryServiceableAsync(Guid.NewGuid(), Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Serviceability.CityNotFound");
    }

    [Fact]
    public async Task Service_serviceability_follows_the_localitys_parent_pincode()
    {
        var state = new State(Guid.NewGuid(), "Delhi", "DL" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "New Delhi");
        var zone = new Zone(Guid.NewGuid(), city.Id, "Central");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, Guid.NewGuid().ToString("N")[..6]);
        var locality = new Locality(Guid.NewGuid(), zone.Id, pincode.Id, "Connaught Place");
        var category = new Category(Guid.NewGuid(), "Electrician", "electrician-" + Guid.NewGuid(), "desc");
        var svc = new Service(Guid.NewGuid(), category.Id, "Wiring Check", "desc", 299m);

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.Zones.Add(zone);
            context.Pincodes.Add(pincode);
            context.Localities.Add(locality);
            context.Add(category);
            context.Add(svc);
            context.ServicePincodeMappings.Add(new ServicePincodeMapping(Guid.NewGuid(), svc.Id, pincode.Id));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(readContext));

        var result = await service.IsServiceServiceableByLocalityAsync(svc.Id, locality.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_locality_returns_a_not_found_error()
    {
        using var context = _db.CreateContext();
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(context));

        var result = await service.IsServiceServiceableByLocalityAsync(Guid.NewGuid(), Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Serviceability.LocalityNotFound");
    }

    [Fact]
    public async Task Inactive_pincode_mapping_is_reported_not_serviceable()
    {
        var state = new State(Guid.NewGuid(), "Tamil Nadu", "TN" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Chennai");
        var pincode = new Pincode(Guid.NewGuid(), city.Id, Guid.NewGuid().ToString("N")[..6]);
        var category = new Category(Guid.NewGuid(), "Carpentry", "carpentry-" + Guid.NewGuid(), "desc");
        var svc = new Service(Guid.NewGuid(), category.Id, "Furniture Repair", "desc", 399m);
        var mapping = new ServicePincodeMapping(Guid.NewGuid(), svc.Id, pincode.Id);
        mapping.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.Pincodes.Add(pincode);
            context.Add(category);
            context.Add(svc);
            context.ServicePincodeMappings.Add(mapping);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(readContext));

        var result = await service.IsServiceServiceableByPincodeAsync(svc.Id, pincode.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }
}
