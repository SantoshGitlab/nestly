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

/// <summary>Covers the Phase 3 catalog redesign's admin add-on-group management: CRUD, delete-while-in-use conflict, audit trail.</summary>
public sealed class ServiceAddOnGroupManagementServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceAddOnGroupManagementServiceTests(TestDatabase db) => _db = db;

    private static ServiceAddOnGroupManagementService CreateService(NestlyDbContext context) =>
        new(
            new ServiceAddOnGroupRepository(context),
            new ServiceAddOnRepository(context),
            new ServiceRepository(context),
            new AuditLogWriter(context, new StubAuditContextProvider()),
            new InMemoryCacheService());

    private static async Task<Service> SeedServiceAsync(NestlyDbContext context)
    {
        var categoryRepository = new CategoryRepository(context);
        var category = new Category(Guid.NewGuid(), "Cleaning", $"cleaning-{Guid.NewGuid():N}", "desc");
        await categoryRepository.AddAsync(category);

        var serviceRepository = new ServiceRepository(context);
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Cleaning", $"deep-cleaning-{Guid.NewGuid():N}", "desc", 999m);
        await serviceRepository.AddAsync(service);
        return service;
    }

    private static ServiceAddOnGroupCreateRequest ValidCreateRequest(Guid serviceId) => new(
        ServiceId: serviceId, Name: "Choose a detergent", SelectionType: nameof(AddOnGroupSelectionType.Single),
        MinSelect: 0, MaxSelect: 1, SortOrder: 1);

    [Fact]
    public async Task Creating_a_group_mapped_to_a_service_then_listing_returns_it_with_audit_entry()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var service = CreateService(context);

        var created = await service.CreateAsync(ValidCreateRequest(svc.Id));

        created.IsSuccess.Should().BeTrue();
        created.Value.ServiceName.Should().Be(svc.Name);

        var groups = await service.ListAsync(svc.Id);
        groups.Should().Contain(g => g.Id == created.Value.Id);

        context.Set<AuditLog>().Should().Contain(a => a.EntityName == "ServiceAddOnGroup" && a.EntityId == created.Value.Id.ToString() && a.Action == "Created");
    }

    [Fact]
    public async Task Creating_a_group_under_an_unknown_service_returns_not_found()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        var result = await service.CreateAsync(ValidCreateRequest(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Service.NotFound");
    }

    [Fact]
    public async Task Creating_a_single_selection_group_with_max_select_above_one_is_rejected()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var service = CreateService(context);

        var request = ValidCreateRequest(svc.Id) with { MaxSelect = 2 };
        var result = await service.CreateAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ServiceAddOnGroup.InvalidSelectionRule");
    }

    [Fact]
    public async Task Updating_a_group_persists_every_field()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(ValidCreateRequest(svc.Id))).Value;

        var updateRequest = new ServiceAddOnGroupUpdateRequest(
            ServiceId: svc.Id, Name: "Pick your detergent", SelectionType: nameof(AddOnGroupSelectionType.Single),
            MinSelect: 1, MaxSelect: 1, SortOrder: 2);
        var updated = await service.UpdateAsync(created.Id, updateRequest);

        updated.IsSuccess.Should().BeTrue();
        updated.Value.Name.Should().Be("Pick your detergent");
        updated.Value.MinSelect.Should().Be(1);
    }

    [Fact]
    public async Task Deleting_a_group_still_referenced_by_an_addon_is_rejected_as_a_conflict()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var groupService = CreateService(context);
        var group = (await groupService.CreateAsync(ValidCreateRequest(svc.Id))).Value;

        var addOnRepository = new ServiceAddOnRepository(context);
        var addOn = new ServiceAddOn(Guid.NewGuid(), svc.Id, "Liquid Detergent", 49m);
        addOn.SetGroupId(group.Id);
        await addOnRepository.AddAsync(addOn);

        var result = await groupService.DeleteAsync(group.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ServiceAddOnGroup.InUse");
    }

    [Fact]
    public async Task Deleting_a_group_with_no_addons_succeeds()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(ValidCreateRequest(svc.Id))).Value;

        (await service.DeleteAsync(created.Id)).IsSuccess.Should().BeTrue();

        (await service.ListAsync(svc.Id)).Should().NotContain(g => g.Id == created.Id);
    }

    [Fact]
    public async Task Operating_on_an_unknown_group_returns_not_found()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        (await service.GetByIdAsync(Guid.NewGuid())).Error.Code.Should().Be("ServiceAddOnGroup.NotFound");
        (await service.DeleteAsync(Guid.NewGuid())).Error.Code.Should().Be("ServiceAddOnGroup.NotFound");
    }

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, Guid.NewGuid(), IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }
}
