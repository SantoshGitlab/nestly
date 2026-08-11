using FluentAssertions;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Catalog;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Catalog.Tests;

/// <summary>Covers task 107: admin add-on management and service mapping (SRS 12.7).</summary>
public sealed class ServiceAddOnManagementServiceTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceAddOnManagementServiceTests(TestDatabase db) => _db = db;

    private static ServiceAddOnManagementService CreateService(NestlyDbContext context) =>
        new(
            new ServiceAddOnRepository(context),
            new ServiceRepository(context),
            new ServiceAddOnGroupRepository(context),
            new AuditLogWriter(context, new StubAuditContextProvider()));

    private static async Task<Service> SeedServiceAsync(NestlyDbContext context)
    {
        var categoryRepository = new CategoryRepository(context);
        var category = new Category(Guid.NewGuid(), "Cleaning", $"cleaning-{Guid.NewGuid():N}", "Cleaning services");
        await categoryRepository.AddAsync(category);

        var serviceRepository = new ServiceRepository(context);
        var service = new Service(Guid.NewGuid(), category.Id, "Deep Cleaning", $"deep-cleaning-{Guid.NewGuid():N}", "Deep cleaning", 999m);
        await serviceRepository.AddAsync(service);
        return service;
    }

    private static ServiceAddOnCreateRequest ValidCreateRequest(Guid serviceId) => new(
        ServiceId: serviceId,
        Name: "Fridge Cleaning",
        Description: "Interior and exterior fridge cleaning.",
        Price: 199m,
        SortOrder: 1,
        IsQuantityAllowed: false,
        IsMandatory: false);

    [Fact]
    public async Task Creating_an_addon_mapped_to_a_service_then_listing_returns_it_with_audit_entry()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var service = CreateService(context);

        var created = await service.CreateAsync(ValidCreateRequest(svc.Id));

        created.IsSuccess.Should().BeTrue();
        created.Value.ServiceName.Should().Be(svc.Name);

        var addOns = await service.ListAsync(svc.Id);
        addOns.Should().Contain(a => a.Id == created.Value.Id);

        context.Set<AuditLog>().Should().Contain(a => a.EntityName == "ServiceAddOn" && a.EntityId == created.Value.Id.ToString() && a.Action == "Created");
    }

    [Fact]
    public async Task Creating_an_addon_under_an_unknown_service_returns_not_found()
    {
        using var context = _db.CreateContext();
        var service = CreateService(context);

        var result = await service.CreateAsync(ValidCreateRequest(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Service.NotFound");
    }

    [Fact]
    public async Task Updating_an_addon_can_remap_it_to_a_different_service()
    {
        using var context = _db.CreateContext();
        var svc1 = await SeedServiceAsync(context);
        var svc2 = await SeedServiceAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(ValidCreateRequest(svc1.Id))).Value;

        var updateRequest = new ServiceAddOnUpdateRequest(
            ServiceId: svc2.Id,
            Name: "Fridge Deep Cleaning",
            Description: created.Description,
            Price: 249m,
            SortOrder: created.SortOrder,
            IsQuantityAllowed: true,
            IsMandatory: true);

        var updated = await service.UpdateAsync(created.Id, updateRequest);

        updated.IsSuccess.Should().BeTrue();
        updated.Value.ServiceId.Should().Be(svc2.Id);
        updated.Value.ServiceName.Should().Be(svc2.Name);
        updated.Value.IsMandatory.Should().BeTrue();
        updated.Value.IsQuantityAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivating_an_addon_persists_and_audits()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var service = CreateService(context);
        var created = (await service.CreateAsync(ValidCreateRequest(svc.Id))).Value;

        (await service.SetActiveAsync(created.Id, false)).IsSuccess.Should().BeTrue();

        (await service.GetByIdAsync(created.Id)).Value.IsActive.Should().BeFalse();
        context.Set<AuditLog>().Should().Contain(a => a.EntityId == created.Id.ToString() && a.Action == "Deactivated");
    }

    [Fact]
    public async Task Creating_an_addon_with_a_group_belonging_to_the_same_service_persists_the_group()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var groupRepository = new ServiceAddOnGroupRepository(context);
        var group = new ServiceAddOnGroup(Guid.NewGuid(), svc.Id, "Detergent", AddOnGroupSelectionType.Single);
        await groupRepository.AddAsync(group);
        var service = CreateService(context);

        var request = ValidCreateRequest(svc.Id) with { GroupId = group.Id };
        var created = await service.CreateAsync(request);

        created.IsSuccess.Should().BeTrue();
        created.Value.GroupId.Should().Be(group.Id);
    }

    [Fact]
    public async Task Creating_an_addon_with_a_group_belonging_to_a_different_service_is_rejected()
    {
        using var context = _db.CreateContext();
        var svc1 = await SeedServiceAsync(context);
        var svc2 = await SeedServiceAsync(context);
        var groupRepository = new ServiceAddOnGroupRepository(context);
        var group = new ServiceAddOnGroup(Guid.NewGuid(), svc2.Id, "Extras", AddOnGroupSelectionType.Multiple);
        await groupRepository.AddAsync(group);
        var service = CreateService(context);

        var request = ValidCreateRequest(svc1.Id) with { GroupId = group.Id };
        var result = await service.CreateAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ServiceAddOnGroup.ServiceMismatch");
    }

    [Fact]
    public async Task Creating_an_addon_with_an_unknown_group_returns_not_found()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var service = CreateService(context);

        var request = ValidCreateRequest(svc.Id) with { GroupId = Guid.NewGuid() };
        var result = await service.CreateAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ServiceAddOnGroup.NotFound");
    }

    [Fact]
    public async Task An_addon_created_without_a_group_stays_ungrouped()
    {
        using var context = _db.CreateContext();
        var svc = await SeedServiceAsync(context);
        var service = CreateService(context);

        var created = await service.CreateAsync(ValidCreateRequest(svc.Id));

        created.Value.GroupId.Should().BeNull();
    }

    private sealed class StubAuditContextProvider : IAuditContextProvider
    {
        public AuditContext GetCurrent() =>
            new(AuditActorType.AdminUser, Guid.NewGuid(), IpAddress: "127.0.0.1", CorrelationId: "test-correlation-id");
    }
}
