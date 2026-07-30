using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 41: public category APIs (list by city, detail with services/add-ons).</summary>
public sealed class CategoryQueryServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public CategoryQueryServiceTests(TestDatabase db) => _db = db;

    private CategoryQueryService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) => new(
        new CategoryRepository(context),
        new ServiceRepository(context),
        new ServiceAddOnRepository(context),
        new ServiceabilityRepository(context),
        new InMemoryCacheService());

    [Fact]
    public async Task Listing_by_city_returns_only_categories_actively_mapped_to_that_city()
    {
        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        var mappedCategory = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var unmappedCategory = new Category(Guid.NewGuid(), "Painting", "painting-" + Guid.NewGuid(), "desc");

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.Add(mappedCategory);
            context.Add(unmappedCategory);
            context.CategoryCityMappings.Add(new CategoryCityMapping(Guid.NewGuid(), mappedCategory.Id, city.Id));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).ListServiceableInCityAsync(city.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(c => c.Id == mappedCategory.Id);
    }

    [Fact]
    public async Task Listing_for_an_unknown_city_returns_not_found()
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).ListServiceableInCityAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.CityNotFound");
    }

    [Fact]
    public async Task Detail_includes_active_services_with_their_active_addons_only()
    {
        var category = new Category(Guid.NewGuid(), "Salon", "salon-" + Guid.NewGuid(), "desc");
        var activeService = new Service(Guid.NewGuid(), category.Id, "Haircut", "haircut-" + Guid.NewGuid(), "desc", 299m);
        var inactiveService = new Service(Guid.NewGuid(), category.Id, "Old Service", "old-" + Guid.NewGuid(), "desc", 199m);
        inactiveService.Deactivate();
        var activeAddOn = new ServiceAddOn(Guid.NewGuid(), activeService.Id, "Head Massage", 99m);
        var inactiveAddOn = new ServiceAddOn(Guid.NewGuid(), activeService.Id, "Old Addon", 49m);
        inactiveAddOn.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(activeService);
            context.Add(inactiveService);
            context.Add(activeAddOn);
            context.Add(inactiveAddOn);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(category.Slug);

        result.IsSuccess.Should().BeTrue();
        result.Value.Services.Should().ContainSingle(s => s.Id == activeService.Id);
        var service = result.Value.Services.Single();
        service.AddOns.Should().ContainSingle(a => a.Id == activeAddOn.Id);
    }

    [Fact]
    public async Task Detail_for_an_unknown_slug_returns_not_found()
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).GetDetailBySlugAsync("does-not-exist");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.CategoryNotFound");
    }
}
