using System.Diagnostics;
using FluentAssertions;
using Nestly.Application.Catalog;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Performance.Tests;

/// <summary>
/// Task 135a: load/performance testing for catalog browse (SRS 29.1-29.2).
/// Covers the same endpoints task 134's rate-limiting rollout already
/// treats as load-sensitive - GET /catalog/search (CatalogSearchController)
/// and GET /services (ServicesController), plus the category browse path
/// (CategoriesController) - by driving many concurrent requests straight at
/// the underlying application services, each on its own DbContext/connection
/// (see PerfTestDatabase's doc comment for why a file-based database is used
/// instead of Catalog.Tests' shared in-memory one).
///
/// Each concurrent call builds its own service instances (including its own
/// InMemoryCacheService), so every call is a genuine cache miss hitting the
/// database - the realistic worst case for a load test under promotional
/// traffic where nothing is warm yet, and it sidesteps InMemoryCacheService's
/// deliberate lack of thread safety (see that file's doc comment).
/// </summary>
public sealed class CatalogBrowsePerformanceTests : IClassFixture<PerfTestDatabase>
{
    private readonly PerfTestDatabase _db;

    public CatalogBrowsePerformanceTests(PerfTestDatabase db) => _db = db;

    private sealed record Fixture(Guid CityId, string Keyword, IReadOnlyList<Category> Categories, IReadOnlyList<Service> Services);

    /// <summary>
    /// Seeds <paramref name="categoryCount"/> categories, each with
    /// <paramref name="servicesPerCategory"/> active services, all mapped
    /// serviceable in one city. Every name is tagged with a keyword unique to
    /// this call: PerfTestDatabase is an <see cref="IClassFixture{TFixture}"/>
    /// shared across every test method in the class (matching
    /// Catalog.Tests/TestDatabase's convention), so without a unique tag a
    /// free-text search in one test would also match categories/services
    /// left behind by another test method that ran earlier against the same
    /// database.
    /// </summary>
    private Fixture SeedCatalog(int categoryCount, int servicesPerCategory)
    {
        using var context = _db.CreateContext();

        string keyword = "kw" + Guid.NewGuid().ToString("N")[..8];

        var state = new State(Guid.NewGuid(), "Karnataka", "KA" + Guid.NewGuid().ToString("N")[..6]);
        var city = new City(Guid.NewGuid(), state.Id, "Bengaluru");
        context.States.Add(state);
        context.Cities.Add(city);

        var categories = new List<Category>(categoryCount);
        var services = new List<Service>(categoryCount * servicesPerCategory);

        for (int c = 0; c < categoryCount; c++)
        {
            var category = new Category(Guid.NewGuid(), $"Home {keyword} {c}", $"home-{keyword}-{c}-" + Guid.NewGuid().ToString("N")[..6], "desc");
            context.Add(category);
            context.CategoryCityMappings.Add(new CategoryCityMapping(Guid.NewGuid(), category.Id, city.Id));
            categories.Add(category);

            for (int s = 0; s < servicesPerCategory; s++)
            {
                var service = new Service(
                    Guid.NewGuid(), category.Id, $"Deep {keyword} Service {c}-{s}",
                    $"deep-{keyword}-{c}-{s}-" + Guid.NewGuid().ToString("N")[..6], "desc", 899m);
                context.Add(service);
                services.Add(service);
            }
        }

        context.SaveChanges();
        return new Fixture(city.Id, keyword, categories, services);
    }

    private static CatalogSearchService BuildSearchService(NestlyDbContext context) =>
        new(new CategoryRepository(context), new ServiceRepository(context));

    private static ServiceQueryService BuildServiceQueryService(NestlyDbContext context) => new(
        new CategoryRepository(context), new ServiceRepository(context), new ServiceAddOnRepository(context),
        new ServiceFaqRepository(context), new ReviewRepository(context), new InMemoryCacheService());

    private static CategoryQueryService BuildCategoryQueryService(NestlyDbContext context) => new(
        new CategoryRepository(context), new ServiceRepository(context), new ServiceAddOnRepository(context),
        new ServiceabilityRepository(context), new InMemoryCacheService());

    /// <summary>
    /// Seeds well past CatalogSearchService's 20-per-type result cap (task
    /// 136c) so this doubles as the load-bearing regression check for that
    /// cap: a promoted, common search term must never hand back the entire
    /// catalog on every one of many concurrent requests.
    /// </summary>
    [Fact]
    public async Task Concurrent_catalog_searches_all_succeed_and_stay_bounded_by_the_result_cap_under_load()
    {
        const int categoryCount = 30;
        const int servicesPerCategory = 10;
        const int concurrentSearches = 100;
        const int expectedCap = 20;

        var fixture = SeedCatalog(categoryCount, servicesPerCategory);

        var tasks = Enumerable.Range(0, concurrentSearches).Select(async _ =>
        {
            using var context = _db.CreateContext();
            return await BuildSearchService(context).SearchAsync(fixture.Keyword);
        });

        var stopwatch = Stopwatch.StartNew();
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        results.Should().OnlyContain(r => r.IsSuccess);
        results.Should().OnlyContain(
            r => r.Value.Categories.Count == expectedCap,
            $"the seeded catalog has {categoryCount} matching categories but the search endpoint must cap its response, not return everything");
        results.Should().OnlyContain(r => r.Value.Services.Count == expectedCap);

        // Soft load-characteristic assertion (regression guard, not a strict
        // benchmark): 100 concurrent full-catalog searches against a
        // file-based SQLite database.
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task Concurrent_service_by_category_browsing_all_succeed_under_load()
    {
        var fixture = SeedCatalog(categoryCount: 10, servicesPerCategory: 15);
        const int concurrentBrowsers = 80;

        var tasks = Enumerable.Range(0, concurrentBrowsers).Select(async i =>
        {
            var category = fixture.Categories[i % fixture.Categories.Count];
            using var context = _db.CreateContext();
            return await BuildServiceQueryService(context).ListByCategoryAsync(category.Id);
        });

        var stopwatch = Stopwatch.StartNew();
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        results.Should().OnlyContain(r => r.IsSuccess);
        results.Should().OnlyContain(r => r.Value.Count == 15);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task Concurrent_category_browsing_in_a_city_all_succeed_under_load()
    {
        var fixture = SeedCatalog(categoryCount: 20, servicesPerCategory: 5);
        const int concurrentBrowsers = 80;

        var tasks = Enumerable.Range(0, concurrentBrowsers).Select(async _ =>
        {
            using var context = _db.CreateContext();
            return await BuildCategoryQueryService(context).ListServiceableInCityAsync(fixture.CityId);
        });

        var stopwatch = Stopwatch.StartNew();
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        results.Should().OnlyContain(r => r.IsSuccess);
        results.Should().OnlyContain(r => r.Value.Count == 20);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20));
    }
}
