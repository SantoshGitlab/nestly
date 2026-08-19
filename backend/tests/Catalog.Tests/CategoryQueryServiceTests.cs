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
        new ServiceGroupRepository(context),
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

    /// <summary>
    /// Bidirectional to the "add locations" work: a category mapped to the
    /// city is still only shown for a specific pincode if one of its
    /// services actually reaches that pincode (SRS 11.1.3) - city mapping
    /// alone is not enough once an area is picked.
    /// </summary>
    [Fact]
    public async Task Listing_narrowed_to_a_pincode_excludes_a_category_with_no_service_reaching_it()
    {
        var state = new State(Guid.NewGuid(), "Rajasthan", "RJ" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Jaipur");
        var reachablePincode = new Pincode(Guid.NewGuid(), city.Id, "302017");
        var unreachablePincode = new Pincode(Guid.NewGuid(), city.Id, "302020");

        var reachableCategory = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var unreachableCategory = new Category(Guid.NewGuid(), "Painting", "painting-" + Guid.NewGuid(), "desc");
        var reachableService = new Service(Guid.NewGuid(), reachableCategory.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 999m);
        var unreachableService = new Service(Guid.NewGuid(), unreachableCategory.Id, "Paint Job", "paint-job-" + Guid.NewGuid(), "desc", 1999m);

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.Pincodes.AddRange(reachablePincode, unreachablePincode);
            context.Add(reachableCategory);
            context.Add(unreachableCategory);
            context.Add(reachableService);
            context.Add(unreachableService);
            context.CategoryCityMappings.Add(new CategoryCityMapping(Guid.NewGuid(), reachableCategory.Id, city.Id));
            context.CategoryCityMappings.Add(new CategoryCityMapping(Guid.NewGuid(), unreachableCategory.Id, city.Id));
            context.ServicePincodeMappings.Add(new ServicePincodeMapping(Guid.NewGuid(), reachableService.Id, reachablePincode.Id));
            // unreachableService has no mapping to either pincode at all.
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).ListServiceableInCityAsync(city.Id, reachablePincode.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(c => c.Id == reachableCategory.Id);
    }

    [Fact]
    public async Task Listing_with_no_pincode_returns_every_city_mapped_category_regardless_of_area_reach()
    {
        var state = new State(Guid.NewGuid(), "Rajasthan", "RJ" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Jaipur");
        var category = new Category(Guid.NewGuid(), "Painting", "painting-" + Guid.NewGuid(), "desc");
        // No service, no ServicePincodeMapping at all - purely city-mapped.

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.Add(category);
            context.CategoryCityMappings.Add(new CategoryCityMapping(Guid.NewGuid(), category.Id, city.Id));
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).ListServiceableInCityAsync(city.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(c => c.Id == category.Id);
    }

    [Fact]
    public async Task Listing_for_an_unknown_pincode_returns_not_found()
    {
        var state = new State(Guid.NewGuid(), "Rajasthan", "RJ" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Jaipur");

        using (var context = _db.CreateContext())
        {
            context.States.Add(state);
            context.Cities.Add(city);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).ListServiceableInCityAsync(city.Id, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.PincodeNotFound");
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
    public async Task Detail_includes_a_services_cover_image_and_duration()
    {
        var category = new Category(Guid.NewGuid(), "Repairs3", "repairs-3-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "AC Repair", "ac-repair-2-" + Guid.NewGuid(), "desc", 499m);
        service.SetCoverImageUrl("https://picsum.photos/seed/ac-repair/640/480");
        service.SetDuration(75);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(category.Slug);

        var found = result.Value.Services.Should().ContainSingle(s => s.Id == service.Id).Subject;
        found.CoverImageUrl.Should().Be("https://picsum.photos/seed/ac-repair/640/480");
        found.DurationMinutes.Should().Be(75);
    }

    [Fact]
    public async Task Detail_for_an_unknown_slug_returns_not_found()
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).GetDetailBySlugAsync("does-not-exist");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.CategoryNotFound");
    }

    [Fact]
    public async Task Detail_includes_active_subcategories_and_excludes_inactive_ones()
    {
        var parent = new Category(Guid.NewGuid(), "Cleaning2", "cleaning-2-" + Guid.NewGuid(), "desc");
        var activeChild = new Category(Guid.NewGuid(), "Kitchen Cleaning", "kitchen-cleaning-" + Guid.NewGuid(), "desc");
        activeChild.SetParent(parent.Id);
        var inactiveChild = new Category(Guid.NewGuid(), "Bathroom Cleaning", "bathroom-cleaning-" + Guid.NewGuid(), "desc");
        inactiveChild.SetParent(parent.Id);
        inactiveChild.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.Add(parent);
            context.Add(activeChild);
            context.Add(inactiveChild);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(parent.Slug);

        result.Value.Subcategories.Should().ContainSingle(c => c.Id == activeChild.Id);
    }

    [Fact]
    public async Task Detail_for_a_category_with_no_children_returns_an_empty_subcategory_list()
    {
        var category = new Category(Guid.NewGuid(), "Repairs2", "repairs-2-" + Guid.NewGuid(), "desc");

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(category.Slug);

        result.Value.Subcategories.Should().BeEmpty();
    }

    [Fact]
    public async Task Detail_for_a_subcategory_is_not_found_once_its_parent_is_deactivated()
    {
        var parent = new Category(Guid.NewGuid(), "Home Cleaning4", "home-cleaning-4-" + Guid.NewGuid(), "desc");
        var child = new Category(Guid.NewGuid(), "Kitchen Cleaning4", "kitchen-cleaning-4-" + Guid.NewGuid(), "desc");
        child.SetParent(parent.Id);
        parent.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.Add(parent);
            context.Add(child);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(child.Slug);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.CategoryNotFound");
    }

    [Fact]
    public async Task Detail_surfaces_grouped_services_under_their_group_and_excludes_them_from_the_flat_list()
    {
        var category = new Category(Guid.NewGuid(), "AC5", "ac-5-" + Guid.NewGuid(), "desc");
        var group = new ServiceGroup(Guid.NewGuid(), category.Id, "Repair & gas refill");
        var grouped = new Service(Guid.NewGuid(), category.Id, "AC Repair", "ac-repair-5-" + Guid.NewGuid(), "desc", 199m);
        grouped.SetServiceGroupId(group.Id);
        var ungrouped = new Service(Guid.NewGuid(), category.Id, "Installation", "installation-5-" + Guid.NewGuid(), "desc", 499m);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(group);
            context.Add(grouped);
            context.Add(ungrouped);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(category.Slug);

        result.Value.ServiceGroups.Should().ContainSingle(g => g.Id == group.Id);
        var groupResponse = result.Value.ServiceGroups.Single();
        groupResponse.Name.Should().Be("Repair & gas refill");
        groupResponse.Services.Should().ContainSingle(s => s.Id == grouped.Id);

        result.Value.Services.Should().ContainSingle(s => s.Id == ungrouped.Id);
        result.Value.Services.Should().NotContain(s => s.Id == grouped.Id);
    }

    [Fact]
    public async Task Detail_for_a_category_with_no_service_groups_returns_an_empty_group_list_and_all_services_ungrouped()
    {
        var category = new Category(Guid.NewGuid(), "Washing Machine5", "washing-machine-5-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Washing machine jet service", "wm-jet-5-" + Guid.NewGuid(), "desc", 299m);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(category.Slug);

        result.Value.ServiceGroups.Should().BeEmpty();
        result.Value.Services.Should().ContainSingle(s => s.Id == service.Id);
    }

    [Fact]
    public async Task Detail_excludes_a_group_with_no_active_services_from_the_group_list()
    {
        var category = new Category(Guid.NewGuid(), "AC6", "ac-6-" + Guid.NewGuid(), "desc");
        var emptyGroup = new ServiceGroup(Guid.NewGuid(), category.Id, "Super saver packages");

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(emptyGroup);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(category.Slug);

        result.Value.ServiceGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Detail_hides_a_service_whose_group_has_been_deactivated()
    {
        var category = new Category(Guid.NewGuid(), "AC7", "ac-7-" + Guid.NewGuid(), "desc");
        var group = new ServiceGroup(Guid.NewGuid(), category.Id, "Repair & gas refill");
        group.Deactivate();
        var service = new Service(Guid.NewGuid(), category.Id, "AC Repair", "ac-repair-7-" + Guid.NewGuid(), "desc", 199m);
        service.SetServiceGroupId(group.Id);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(group);
            context.Add(service);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(category.Slug);

        result.Value.ServiceGroups.Should().BeEmpty();
        result.Value.Services.Should().BeEmpty();
    }
}
