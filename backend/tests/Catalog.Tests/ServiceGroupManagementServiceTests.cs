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

/// <summary>Covers admin service-group management: CRUD, activation, delete-while-in-use conflict, audit trail, explicit category-cache invalidation.</summary>
public sealed class ServiceGroupManagementServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceGroupManagementServiceTests(TestDatabase db) => _db = db;

    private static ServiceGroupManagementService CreateService(NestlyDbContext context, InMemoryCacheService? cache = null) =>
        new(
            new ServiceGroupRepository(context),
            new ServiceRepository(context),
            new CategoryRepository(context),
            new AuditLogWriter(context, new StubAuditContextProvider()),
            cache ?? new InMemoryCacheService());

    private static async Task<Category> SeedCategoryAsync(NestlyDbContext context)
    {
        var categoryRepository = new CategoryRepository(context);
        var category = new Category(Guid.NewGuid(), "AC", $"ac-{Guid.NewGuid():N}", "desc");
        await categoryRepository.AddAsync(category);
        return category;
    }

    private static ServiceGroupCreateRequest ValidCreateRequest(Guid categoryId) => new(
        CategoryId: categoryId, Name: "Repair & gas refill", SortOrder: 1);

    [Fact]
    public async Task Creating_a_group_mapped_to_a_category_then_listing_returns_it_with_audit_entry_and_busts_cache()
    {
        using var context = _db.CreateContext();
        var category = await SeedCategoryAsync(context);
        var cache = new InMemoryCacheService();
        await cache.SetAsync(CacheKeys.Category(category.Id), "stale");
        var service = CreateService(context, cache);

        var created = await service.CreateAsync(ValidCreateRequest(category.Id));

        created.IsSuccess.Should().BeTrue();
        created.Value.CategoryName.Should().Be(category.Name);

        var groups = await service.ListAsync(category.Id);
        groups.Should().Contain(g => g.Id == created.Value.Id);

        context.Set<AuditLog>().Should().Contain(a => a.EntityName == "ServiceGroup" && a.EntityId == created.Value.Id.ToString() && a.Action == "Created");
        (await cache.GetAsync<string>(CacheKeys.Category(category.Id))).Should().BeNull();
    }

    [Fact]
    public async Task Creating_a_group_under_an_unknown_category_returns_not_found()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        var result = await service.CreateAsync(ValidCreateRequest(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Category.NotFound");
    }

    [Fact]
    public async Task Updating_a_group_persists_every_field()
    {
        using var context = _db.CreateContext();
        var category = await SeedCategoryAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(ValidCreateRequest(category.Id))).Value;

        var updateRequest = new ServiceGroupUpdateRequest(CategoryId: category.Id, Name: "Service", SortOrder: 2);
        var updated = await service.UpdateAsync(created.Id, updateRequest);

        updated.IsSuccess.Should().BeTrue();
        updated.Value.Name.Should().Be("Service");
        updated.Value.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task Deactivating_then_reactivating_a_group_toggles_is_active()
    {
        using var context = _db.CreateContext();
        var category = await SeedCategoryAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(ValidCreateRequest(category.Id))).Value;

        (await service.SetActiveAsync(created.Id, false)).IsSuccess.Should().BeTrue();
        (await service.GetByIdAsync(created.Id)).Value.IsActive.Should().BeFalse();

        (await service.SetActiveAsync(created.Id, true)).IsSuccess.Should().BeTrue();
        (await service.GetByIdAsync(created.Id)).Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_group_still_referenced_by_a_service_is_rejected_as_a_conflict()
    {
        using var context = _db.CreateContext();
        var category = await SeedCategoryAsync(context);
        var groupService = CreateService(context);
        var group = (await groupService.CreateAsync(ValidCreateRequest(category.Id))).Value;

        var serviceRepository = new ServiceRepository(context);
        var svc = new Service(Guid.NewGuid(), category.Id, "AC Repair", $"ac-repair-{Guid.NewGuid():N}", "desc", 499m);
        svc.SetServiceGroupId(group.Id);
        await serviceRepository.AddAsync(svc);

        var result = await groupService.DeleteAsync(group.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ServiceGroup.InUse");
    }

    [Fact]
    public async Task Deleting_a_group_with_no_services_succeeds()
    {
        using var context = _db.CreateContext();
        var category = await SeedCategoryAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(ValidCreateRequest(category.Id))).Value;

        (await service.DeleteAsync(created.Id)).IsSuccess.Should().BeTrue();

        (await service.ListAsync(category.Id)).Should().NotContain(g => g.Id == created.Id);
    }

    [Fact]
    public async Task Operating_on_an_unknown_group_returns_not_found()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        (await service.GetByIdAsync(Guid.NewGuid())).Error.Code.Should().Be("ServiceGroup.NotFound");
        (await service.SetActiveAsync(Guid.NewGuid(), true)).Error.Code.Should().Be("ServiceGroup.NotFound");
        (await service.DeleteAsync(Guid.NewGuid())).Error.Code.Should().Be("ServiceGroup.NotFound");
    }

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, Guid.NewGuid(), IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }
}
