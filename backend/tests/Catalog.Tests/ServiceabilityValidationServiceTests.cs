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
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(readContext), new InMemoryCacheService());

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
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(readContext), new InMemoryCacheService());

        var result = await service.IsCategoryServiceableAsync(category.Id, city.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_city_returns_a_not_found_error()
    {
        using var context = _db.CreateContext();
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(context), new InMemoryCacheService());

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
        var svc = new Service(Guid.NewGuid(), category.Id, "Wiring Check", "wiring-check-" + Guid.NewGuid(), "desc", 299m);

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
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(readContext), new InMemoryCacheService());

        var result = await service.IsServiceServiceableByLocalityAsync(svc.Id, locality.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_locality_returns_a_not_found_error()
    {
        using var context = _db.CreateContext();
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(context), new InMemoryCacheService());

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
        var svc = new Service(Guid.NewGuid(), category.Id, "Furniture Repair", "furniture-repair-" + Guid.NewGuid(), "desc", 399m);
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
        var service = new ServiceabilityValidationService(new ServiceabilityRepository(readContext), new InMemoryCacheService());

        var result = await service.IsServiceServiceableByPincodeAsync(svc.Id, pincode.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    /// <summary>
    /// Regression guard for task 49's caching: a naive GetOrCreateAsync
    /// implementation that null-checks a boxed T? to detect a cache hit
    /// cannot distinguish a genuine miss from a legitimately cached `true`
    /// serviceability result for a non-nullable value type like bool - see
    /// DistributedCacheService.GetOrCreateAsync's payload-presence check.
    /// This exercises the real cache (not a stub), across two separately
    /// constructed service instances sharing one ICacheService, to prove the
    /// second call is answered from cache rather than by re-querying a
    /// mapping that has since been deactivated.
    /// </summary>
    [Fact]
    public async Task A_true_result_is_actually_cached_and_survives_the_mapping_later_being_deactivated()
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
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

        var cache = new InMemoryCacheService();

        using (var firstContext = _db.CreateContext())
        {
            var firstResult = await new ServiceabilityValidationService(new ServiceabilityRepository(firstContext), cache)
                .IsCategoryServiceableAsync(category.Id, city.Id);
            firstResult.Value.Should().BeTrue();
        }

        using (var mutateContext = _db.CreateContext())
        {
            mutateContext.CategoryCityMappings.Attach(mapping);
            mapping.Deactivate();
            await mutateContext.SaveChangesAsync();
        }

        using (var secondContext = _db.CreateContext())
        {
            var secondResult = await new ServiceabilityValidationService(new ServiceabilityRepository(secondContext), cache)
                .IsCategoryServiceableAsync(category.Id, city.Id);

            secondResult.Value.Should().BeTrue("the second call must be answered from cache, not by re-querying the now-deactivated mapping");
        }
    }
}
