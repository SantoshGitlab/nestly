using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>Covers the ServiceGroup repository: ordering, active-only batch lookup.</summary>
public sealed class ServiceGroupRepositoryTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceGroupRepositoryTests(TestDatabase db) => _db = db;

    [Fact]
    public async Task ListAllAsync_returns_every_group_for_a_category_including_inactive_ordered_by_sort_order()
    {
        using var context = _db.CreateContext();
        var repository = new ServiceGroupRepository(context);
        var categoryId = Guid.NewGuid();

        var second = new ServiceGroup(Guid.NewGuid(), categoryId, "Repair & gas refill");
        second.SetSortOrder(2);
        var first = new ServiceGroup(Guid.NewGuid(), categoryId, "Super saver packages");
        first.SetSortOrder(1);
        var inactive = new ServiceGroup(Guid.NewGuid(), categoryId, "Service");
        inactive.SetSortOrder(3);
        inactive.Deactivate();

        await repository.AddAsync(second);
        await repository.AddAsync(first);
        await repository.AddAsync(inactive);

        var result = await repository.ListAllAsync(categoryId);

        result.Select(g => g.Id).Should().Equal(first.Id, second.Id, inactive.Id);
    }

    [Fact]
    public async Task ListAllAsync_with_no_category_filter_returns_groups_across_categories()
    {
        using var context = _db.CreateContext();
        var repository = new ServiceGroupRepository(context);

        var groupA = new ServiceGroup(Guid.NewGuid(), Guid.NewGuid(), "Service");
        var groupB = new ServiceGroup(Guid.NewGuid(), Guid.NewGuid(), "Repair");
        await repository.AddAsync(groupA);
        await repository.AddAsync(groupB);

        var result = await repository.ListAllAsync(categoryId: null);

        result.Select(g => g.Id).Should().Contain([groupA.Id, groupB.Id]);
    }

    [Fact]
    public async Task ListActiveByCategoryIdsAsync_batches_across_categories_and_excludes_inactive_groups()
    {
        using var context = _db.CreateContext();
        var repository = new ServiceGroupRepository(context);
        var categoryA = Guid.NewGuid();
        var categoryB = Guid.NewGuid();

        var activeA = new ServiceGroup(Guid.NewGuid(), categoryA, "Super saver packages");
        var inactiveA = new ServiceGroup(Guid.NewGuid(), categoryA, "Old Group");
        inactiveA.Deactivate();
        var activeB = new ServiceGroup(Guid.NewGuid(), categoryB, "Service");

        await repository.AddAsync(activeA);
        await repository.AddAsync(inactiveA);
        await repository.AddAsync(activeB);

        var result = await repository.ListActiveByCategoryIdsAsync([categoryA, categoryB]);

        result.Select(g => g.Id).Should().BeEquivalentTo([activeA.Id, activeB.Id]);
    }

    [Fact]
    public async Task ListActiveByCategoryIdsAsync_returns_empty_for_an_empty_id_set()
    {
        using var context = _db.CreateContext();
        var repository = new ServiceGroupRepository(context);

        (await repository.ListActiveByCategoryIdsAsync([])).Should().BeEmpty();
    }
}
