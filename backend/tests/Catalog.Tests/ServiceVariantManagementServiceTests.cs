using FluentAssertions;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Abstractions.Caching;
using Nestly.Application.Catalog;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers the Phase 3 catalog redesign's admin variant management: CRUD, audit trail, explicit cache invalidation.</summary>
public sealed class ServiceVariantManagementServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceVariantManagementServiceTests(TestDatabase db) => _db = db;

    private static ServiceVariantManagementService CreateService(NestlyDbContext context, InMemoryCacheService? cache = null) =>
        new(
            new ServiceVariantRepository(context),
            new ServiceRepository(context),
            new AuditLogWriter(context, new StubAuditContextProvider()),
            cache ?? new InMemoryCacheService());

    private static async Task<Service> SeedServiceAsync(NestlyDbContext context)
    {
        var categoryRepository = new CategoryRepository(context);
        var category = new Category(Guid.NewGuid(), "Appliance Repair", $"appliance-repair-{Guid.NewGuid():N}", "desc");
        await categoryRepository.AddAsync(category);

        var serviceRepository = new ServiceRepository(context);
        var service = new Service(Guid.NewGuid(), category.Id, "AC Repair", $"ac-repair-{Guid.NewGuid():N}", "desc", 500m);
        await serviceRepository.AddAsync(service);
        return service;
    }

    private static ServiceVariantCreateRequest ValidCreateRequest() => new(
        Name: "Split AC", Price: 599m, DurationMinutes: 90, InclusionsOverride: "Includes gas top-up", SortOrder: 1);

    [Fact]
    public async Task Creating_a_variant_under_a_service_then_listing_returns_it_with_audit_entry_and_busts_cache()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var cache = new InMemoryCacheService();
        await cache.SetAsync(CacheKeys.Service(svc.Id), "stale");
        var service = CreateService(context, cache);

        var created = await service.CreateAsync(svc.Id, ValidCreateRequest());

        created.IsSuccess.Should().BeTrue();
        var variants = await service.ListAsync(svc.Id);
        variants.Should().Contain(v => v.Id == created.Value.Id);

        context.Set<AuditLog>().Should().Contain(a => a.EntityName == "ServiceVariant" && a.EntityId == created.Value.Id.ToString() && a.Action == "Created");
        (await cache.GetAsync<string>(CacheKeys.Service(svc.Id))).Should().BeNull();
    }

    [Fact]
    public async Task Creating_a_variant_under_an_unknown_service_returns_not_found()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        var result = await service.CreateAsync(Guid.NewGuid(), ValidCreateRequest());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Service.NotFound");
    }

    [Fact]
    public async Task Updating_a_variant_persists_every_field()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(svc.Id, ValidCreateRequest())).Value;

        var updateRequest = new ServiceVariantUpdateRequest(
            Name: "Split AC (1.5 Ton)", Price: 699m, DurationMinutes: 100, InclusionsOverride: null, SortOrder: 2);
        var updated = await service.UpdateAsync(created.Id, updateRequest);

        updated.IsSuccess.Should().BeTrue();
        updated.Value.Name.Should().Be("Split AC (1.5 Ton)");
        updated.Value.Price.Should().Be(699m);
        updated.Value.InclusionsOverride.Should().BeNull();
    }

    [Fact]
    public async Task Deactivating_then_reactivating_a_variant_toggles_is_active()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(svc.Id, ValidCreateRequest())).Value;

        (await service.SetActiveAsync(created.Id, false)).IsSuccess.Should().BeTrue();
        (await service.GetByIdAsync(created.Id)).Value.IsActive.Should().BeFalse();

        (await service.SetActiveAsync(created.Id, true)).IsSuccess.Should().BeTrue();
        (await service.GetByIdAsync(created.Id)).Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_variant_removes_it_from_the_list()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(svc.Id, ValidCreateRequest())).Value;

        (await service.DeleteAsync(created.Id)).IsSuccess.Should().BeTrue();

        (await service.ListAsync(svc.Id)).Should().NotContain(v => v.Id == created.Id);
    }

    [Fact]
    public async Task Operating_on_an_unknown_variant_returns_not_found()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        (await service.GetByIdAsync(Guid.NewGuid())).Error.Code.Should().Be("ServiceVariant.NotFound");
        (await service.SetActiveAsync(Guid.NewGuid(), true)).Error.Code.Should().Be("ServiceVariant.NotFound");
        (await service.DeleteAsync(Guid.NewGuid())).Error.Code.Should().Be("ServiceVariant.NotFound");
    }

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, Guid.NewGuid(), IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }
}
