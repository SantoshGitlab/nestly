using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Nestly.Application;
using Nestly.Application.Abstractions.Caching;
using Nestly.Domain;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Caching;
using Nestly.Infrastructure.Persistence.Interceptors;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 49: catalog cache eviction on lifecycle/mapping events.</summary>
public sealed class CatalogCacheInvalidationHandlerTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public CatalogCacheInvalidationHandlerTests(TestDatabase db) => _db = db;

    private static async Task<InMemoryCacheService> SeededCache(params (string Key, string Value)[] entries)
    {
        var cache = new InMemoryCacheService();
        foreach (var (key, value) in entries)
        {
            await cache.SetAsync(key, value);
        }

        return cache;
    }

    [Fact]
    public async Task CategoryCityMappingChangedEvent_evicts_that_categorys_serviceability_in_that_city()
    {
        var categoryId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var key = CacheKeys.CategoryServiceability(categoryId, cityId);
        var cache = await SeededCache((key, "stale"));

        using var context = _db.CreateContext();
        var handler = new CatalogCacheInvalidationHandler(cache, new ServiceRepository(context), new CategoryRepository(context));

        await handler.Handle(new DomainEventNotification<CategoryCityMappingChangedEvent>(
            new CategoryCityMappingChangedEvent(categoryId, cityId)), CancellationToken.None);

        (await cache.GetAsync<string>(key)).Should().BeNull();
    }

    [Fact]
    public async Task ServicePincodeMappingChangedEvent_evicts_that_services_serviceability_in_that_pincode()
    {
        var serviceId = Guid.NewGuid();
        var pincodeId = Guid.NewGuid();
        var key = CacheKeys.ServicePincodeServiceability(serviceId, pincodeId);
        var cache = await SeededCache((key, "stale"));

        using var context = _db.CreateContext();
        var handler = new CatalogCacheInvalidationHandler(cache, new ServiceRepository(context), new CategoryRepository(context));

        await handler.Handle(new DomainEventNotification<ServicePincodeMappingChangedEvent>(
            new ServicePincodeMappingChangedEvent(serviceId, pincodeId)), CancellationToken.None);

        (await cache.GetAsync<string>(key)).Should().BeNull();
    }

    [Fact]
    public async Task CategoryDeactivatedEvent_evicts_that_categorys_detail_cache()
    {
        var categoryId = Guid.NewGuid();
        var key = CacheKeys.Category(categoryId);
        var cache = await SeededCache((key, "stale"));

        using var context = _db.CreateContext();
        var handler = new CatalogCacheInvalidationHandler(cache, new ServiceRepository(context), new CategoryRepository(context));

        await handler.Handle(new DomainEventNotification<CategoryDeactivatedEvent>(
            new CategoryDeactivatedEvent(categoryId)), CancellationToken.None);

        (await cache.GetAsync<string>(key)).Should().BeNull();
    }

    [Fact]
    public async Task ServiceCreatedEvent_evicts_both_the_service_detail_and_its_categorys_service_list()
    {
        var categoryId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var serviceKey = CacheKeys.Service(serviceId);
        var listKey = CacheKeys.ServicesByCategory(categoryId);
        var cache = await SeededCache((serviceKey, "stale"), (listKey, "stale"));

        using var context = _db.CreateContext();
        var handler = new CatalogCacheInvalidationHandler(cache, new ServiceRepository(context), new CategoryRepository(context));

        await handler.Handle(new DomainEventNotification<ServiceCreatedEvent>(
            new ServiceCreatedEvent(serviceId, categoryId)), CancellationToken.None);

        (await cache.GetAsync<string>(serviceKey)).Should().BeNull();
        (await cache.GetAsync<string>(listKey)).Should().BeNull();
    }

    [Fact]
    public async Task ServiceActivatedEvent_evicts_the_service_detail_and_looks_up_its_category_to_evict_that_list_too()
    {
        var category = new Category(Guid.NewGuid(), "Cleaning", "cleaning-" + Guid.NewGuid(), "desc");
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Clean", "deep-clean-" + Guid.NewGuid(), "desc", 499m);

        using (var seedContext = _db.CreateContext())
        {
            seedContext.Add(category);
            seedContext.Add(service);
            seedContext.SaveChanges();
        }

        var serviceKey = CacheKeys.Service(service.Id);
        var listKey = CacheKeys.ServicesByCategory(category.Id);
        var cache = await SeededCache((serviceKey, "stale"), (listKey, "stale"));

        using var readContext = _db.CreateContext();
        var handler = new CatalogCacheInvalidationHandler(cache, new ServiceRepository(readContext), new CategoryRepository(readContext));

        await handler.Handle(new DomainEventNotification<ServiceActivatedEvent>(
            new ServiceActivatedEvent(service.Id)), CancellationToken.None);

        (await cache.GetAsync<string>(serviceKey)).Should().BeNull();
        (await cache.GetAsync<string>(listKey)).Should().BeNull();
    }

    [Fact]
    public async Task ServicePriceChangedEvent_evicts_only_the_service_detail_cache()
    {
        var serviceId = Guid.NewGuid();
        var serviceKey = CacheKeys.Service(serviceId);
        var unrelatedKey = CacheKeys.Category(Guid.NewGuid());
        var cache = await SeededCache((serviceKey, "stale"), (unrelatedKey, "untouched"));

        using var context = _db.CreateContext();
        var handler = new CatalogCacheInvalidationHandler(cache, new ServiceRepository(context), new CategoryRepository(context));

        await handler.Handle(new DomainEventNotification<ServicePriceChangedEvent>(
            new ServicePriceChangedEvent(serviceId, 100m, 150m)), CancellationToken.None);

        (await cache.GetAsync<string>(serviceKey)).Should().BeNull();
        (await cache.GetAsync<string>(unrelatedKey)).Should().Be("untouched");
    }

    /// <summary>
    /// Regression guard: MediatR's own DI registration
    /// (Application.DependencyInjection.AddApplication) only scans the
    /// Application assembly, so this handler - which lives in Infrastructure -
    /// was never reached by a published event until Infrastructure's own
    /// DependencyInjection also called RegisterServicesFromAssembly on itself.
    /// This test resolves IPublisher the same way the composition root does
    /// and confirms an event this handler owns actually reaches it, rather
    /// than unit-testing Handle() directly and assuming MediatR would find it.
    /// </summary>
    [Fact]
    public async Task Handler_is_actually_discovered_by_MediatR_through_the_apps_DI_registration()
    {
        var categoryId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var key = CacheKeys.CategoryServiceability(categoryId, cityId);

        var cache = new InMemoryCacheService();
        await cache.SetAsync(key, "stale");

        using var context = _db.CreateContext();
        var services = new ServiceCollection();
        services.AddSingleton<ICacheService>(cache);
        services.AddSingleton<IServiceRepository>(new ServiceRepository(context));
        services.AddSingleton<ICategoryRepository>(new CategoryRepository(context));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Nestly.Infrastructure.DependencyInjection).Assembly));
        await using var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<IPublisher>();
        await publisher.Publish(new DomainEventNotification<CategoryCityMappingChangedEvent>(
            new CategoryCityMappingChangedEvent(categoryId, cityId)));

        (await cache.GetAsync<string>(key)).Should().BeNull();
    }
}
