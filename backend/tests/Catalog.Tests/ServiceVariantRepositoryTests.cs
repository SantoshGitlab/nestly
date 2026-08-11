using FluentAssertions;
using Nestly.Domain;
using Nestly.Infrastructure.Persistence.Repositories;

namespace Nestly.Catalog.Tests;

/// <summary>Covers the Phase 3 catalog redesign's ServiceVariant repository: ordering, active-only batch lookup.</summary>
public sealed class ServiceVariantRepositoryTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _db;

    public ServiceVariantRepositoryTests(TestDatabase db) => _db = db;

    [Fact]
    public async Task ListByServiceAsync_returns_every_variant_including_inactive_ordered_by_sort_order()
    {
        using var context = _db.CreateContext();
        var repository = new ServiceVariantRepository(context);
        var serviceId = Guid.NewGuid();

        var second = new ServiceVariant(Guid.NewGuid(), serviceId, "Window AC", 399m, 60);
        second.SetSortOrder(2);
        var first = new ServiceVariant(Guid.NewGuid(), serviceId, "Split AC", 599m, 90);
        first.SetSortOrder(1);
        var inactive = new ServiceVariant(Guid.NewGuid(), serviceId, "Cassette AC", 799m, 120);
        inactive.SetSortOrder(3);
        inactive.Deactivate();

        await repository.AddAsync(second);
        await repository.AddAsync(first);
        await repository.AddAsync(inactive);

        var result = await repository.ListByServiceAsync(serviceId);

        result.Select(v => v.Id).Should().Equal(first.Id, second.Id, inactive.Id);
    }

    [Fact]
    public async Task ListActiveByServiceIdsAsync_batches_across_services_and_excludes_inactive_variants()
    {
        using var context = _db.CreateContext();
        var repository = new ServiceVariantRepository(context);
        var serviceA = Guid.NewGuid();
        var serviceB = Guid.NewGuid();

        var activeA = new ServiceVariant(Guid.NewGuid(), serviceA, "Split AC", 599m, 90);
        var inactiveA = new ServiceVariant(Guid.NewGuid(), serviceA, "Old Variant", 399m, 60);
        inactiveA.Deactivate();
        var activeB = new ServiceVariant(Guid.NewGuid(), serviceB, "Window AC", 399m, 60);

        await repository.AddAsync(activeA);
        await repository.AddAsync(inactiveA);
        await repository.AddAsync(activeB);

        var result = await repository.ListActiveByServiceIdsAsync([serviceA, serviceB]);

        result.Select(v => v.Id).Should().BeEquivalentTo([activeA.Id, activeB.Id]);
    }

    [Fact]
    public async Task ListActiveByServiceIdsAsync_returns_empty_for_an_empty_id_set()
    {
        using var context = _db.CreateContext();
        var repository = new ServiceVariantRepository(context);

        (await repository.ListActiveByServiceIdsAsync([])).Should().BeEmpty();
    }
}
