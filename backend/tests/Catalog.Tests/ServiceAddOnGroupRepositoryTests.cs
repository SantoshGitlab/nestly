using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>Covers the Phase 3 catalog redesign's ServiceAddOnGroup repository: filtering, batch lookups.</summary>
public sealed class ServiceAddOnGroupRepositoryTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceAddOnGroupRepositoryTests(TestDatabase db) => _db = db;

    [Fact]
    public async Task ListAllAsync_filters_by_service_when_a_service_id_is_supplied()
    {
        using var context = _db.CreateContext();
        var repository = new ServiceAddOnGroupRepository(context);
        var serviceA = Guid.NewGuid();
        var serviceB = Guid.NewGuid();

        var groupA = new ServiceAddOnGroup(Guid.NewGuid(), serviceA, "Detergent", AddOnGroupSelectionType.Single);
        var groupB = new ServiceAddOnGroup(Guid.NewGuid(), serviceB, "Extras", AddOnGroupSelectionType.Multiple);
        await repository.AddAsync(groupA);
        await repository.AddAsync(groupB);

        var result = await repository.ListAllAsync(serviceA);

        result.Should().ContainSingle(g => g.Id == groupA.Id);
    }

    [Fact]
    public async Task ListAllAsync_returns_every_group_when_no_service_id_is_supplied()
    {
        using var context = _db.CreateContext();
        var repository = new ServiceAddOnGroupRepository(context);
        var groupA = new ServiceAddOnGroup(Guid.NewGuid(), Guid.NewGuid(), "Detergent", AddOnGroupSelectionType.Single);
        var groupB = new ServiceAddOnGroup(Guid.NewGuid(), Guid.NewGuid(), "Extras", AddOnGroupSelectionType.Multiple);
        await repository.AddAsync(groupA);
        await repository.AddAsync(groupB);

        var result = await repository.ListAllAsync(serviceId: null);

        result.Select(g => g.Id).Should().Contain([groupA.Id, groupB.Id]);
    }

    [Fact]
    public async Task GetByIdsAsync_returns_a_dictionary_keyed_by_id()
    {
        using var context = _db.CreateContext();
        var repository = new ServiceAddOnGroupRepository(context);
        var group = new ServiceAddOnGroup(Guid.NewGuid(), Guid.NewGuid(), "Detergent", AddOnGroupSelectionType.Single);
        await repository.AddAsync(group);

        var result = await repository.GetByIdsAsync([group.Id]);

        result.Should().ContainKey(group.Id);
        result[group.Id].Name.Should().Be("Detergent");
    }

    [Fact]
    public async Task GetByIdsAsync_returns_empty_for_an_empty_id_set()
    {
        using var context = _db.CreateContext();
        var repository = new ServiceAddOnGroupRepository(context);

        (await repository.GetByIdsAsync([])).Should().BeEmpty();
    }
}
