using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 42c: combined category/service search.</summary>
public sealed class CatalogSearchServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public CatalogSearchServiceTests(TestDatabase db) => _db = db;

    private CatalogSearchService BuildService(Nestly.Infrastructure.Persistence.NestlyDbContext context) => new(
        new CategoryRepository(context),
        new ServiceRepository(context));

    [Fact]
    public async Task Search_matches_categories_and_services_case_insensitively()
    {
        var category = new Category(Guid.NewGuid(), "Home Cleaning", "home-cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Cleaning Service", "deep-cleaning-" + Guid.NewGuid(), "desc", 899m);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).SearchAsync("cleaning");

        result.IsSuccess.Should().BeTrue();
        result.Value.Categories.Should().Contain(c => c.Id == category.Id);
        result.Value.Services.Should().Contain(s => s.Id == service.Id);
    }

    [Fact]
    public async Task Search_results_include_a_services_cover_image_and_duration()
    {
        var category = new Category(Guid.NewGuid(), "Repairs", "repairs-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Fridge Repair Service", "fridge-repair-" + Guid.NewGuid(), "desc", 499m);
        service.SetCoverImageUrl("https://picsum.photos/seed/fridge/640/480");
        service.SetDuration(45);

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.Add(service);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).SearchAsync("fridge");

        var found = result.Value.Services.Should().ContainSingle(s => s.Id == service.Id).Subject;
        found.CoverImageUrl.Should().Be("https://picsum.photos/seed/fridge/640/480");
        found.DurationMinutes.Should().Be(45);
    }

    [Fact]
    public async Task Search_excludes_inactive_categories_and_services()
    {
        var category = new Category(Guid.NewGuid(), "Old Painting", "old-painting-" + Guid.NewGuid(), "desc");
        category.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.Add(category);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).SearchAsync("painting");

        result.IsSuccess.Should().BeTrue();
        result.Value.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_excludes_a_subcategory_and_its_service_once_the_parent_category_is_deactivated()
    {
        var parent = new Category(Guid.NewGuid(), "Home Cleaning7", "home-cleaning-7-" + Guid.NewGuid(), "desc");
        var subcategory = new Category(Guid.NewGuid(), "Sofa Cleaning7", "sofa-cleaning-7-" + Guid.NewGuid(), "desc");
        subcategory.SetParent(parent.Id);
        var service = new Service(Guid.NewGuid(), subcategory.Id, "Sofa Shampoo Service7", "sofa-shampoo-7-" + Guid.NewGuid(), "desc", 699m);
        parent.Deactivate();

        using (var context = _db.CreateContext())
        {
            context.Add(parent);
            context.Add(subcategory);
            context.Add(service);
            context.SaveChanges();
        }

        using var readContext = _db.CreateContext();
        var result = await BuildService(readContext).SearchAsync("sofa");

        result.IsSuccess.Should().BeTrue();
        result.Value.Categories.Should().BeEmpty();
        result.Value.Services.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public async Task Search_rejects_a_too_short_query(string query)
    {
        using var context = _db.CreateContext();
        var result = await BuildService(context).SearchAsync(query);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Catalog.SearchQueryTooShort");
    }
}
