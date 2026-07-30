using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers tasks 42a/42b: services by category, and service detail with policies.</summary>
public sealed class ServiceQueryServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceQueryServiceTests(TestDatabase db) => _db = db;

    private ServiceQueryService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) => new(
        new CategoryRepository(context),
        new ServiceRepository(context),
        new ServiceAddOnRepository(context));

    [Fact]
    public async Task ListByCategoryAsync_returns_only_active_services_under_that_category()
    {
        var category = new Category(Guid.NewGuid(), "Repairs", "repairs-" + Guid.NewGuid(), "desc");
        var activeService = new Service(Guid.NewGuid(), category.Id, "AC Repair", "ac-repair-" + Guid.NewGuid(), "desc", 499m);
        var inactiveService = new Service(Guid.NewGuid(), category.Id, "Old Repair", "old-repair-" + Guid.NewGuid(), "desc", 199m);
        inactiveService.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(activeService);
            context.Add(inactiveService);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).ListByCategoryAsync(category.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(s => s.Id == activeService.Id);
    }

    [Fact]
    public async Task ListByCategoryAsync_for_an_unknown_category_returns_not_found()
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).ListByCategoryAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.CategoryNotFound");
    }

    [Fact]
    public async Task GetDetailBySlugAsync_includes_policies_addons_and_category_breadcrumb()
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 999m);
        service.SetInclusions("All rooms");
        service.SetExclusions("Balcony");
        service.SetCancellationPolicy("Free cancellation up to 2 hours before slot");
        service.SetReschedulePolicy("One free reschedule");
        var addOn = new ServiceAddOn(Guid.NewGuid(), service.Id, "Sofa Cleaning", 199m);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.Add(addOn);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(service.Slug);

        result.IsSuccess.Should().BeTrue();
        var detail = result.Value;
        detail.Inclusions.Should().Be("All rooms");
        detail.Exclusions.Should().Be("Balcony");
        detail.CancellationPolicy.Should().Be("Free cancellation up to 2 hours before slot");
        detail.ReschedulePolicy.Should().Be("One free reschedule");
        detail.CategorySlug.Should().Be(category.Slug);
        detail.AddOns.Should().ContainSingle(a => a.Id == addOn.Id);
    }

    [Fact]
    public async Task GetDetailBySlugAsync_for_an_inactive_service_returns_not_found()
    {
        var category = new Category(Guid.NewGuid(), "Salon", "salon-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Facial", "facial-" + Guid.NewGuid(), "desc", 599m);
        service.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).GetDetailBySlugAsync(service.Slug);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.ServiceNotFound");
    }
}
