using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Covers the ServiceGroup entity: construction defaults, activation.</summary>
public sealed class ServiceGroupAggregateTests
{
    [Fact]
    public void A_new_group_is_active_with_a_zero_sort_order()
    {
        var group = new ServiceGroup(Guid.NewGuid(), Guid.NewGuid(), "Repair & gas refill");

        group.IsActive.Should().BeTrue();
        group.SortOrder.Should().Be(0);
    }

    [Fact]
    public void Deactivate_then_activate_round_trips_the_flag()
    {
        var group = new ServiceGroup(Guid.NewGuid(), Guid.NewGuid(), "Super saver packages");

        group.Deactivate();
        group.IsActive.Should().BeFalse();

        group.Activate();
        group.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SetSortOrder_updates_the_display_order()
    {
        var group = new ServiceGroup(Guid.NewGuid(), Guid.NewGuid(), "Service");

        group.SetSortOrder(5);

        group.SortOrder.Should().Be(5);
    }

    [Fact]
    public void SetCategoryId_reassigns_the_owning_category()
    {
        var group = new ServiceGroup(Guid.NewGuid(), Guid.NewGuid(), "Service");
        var newCategoryId = Guid.NewGuid();

        group.SetCategoryId(newCategoryId);

        group.CategoryId.Should().Be(newCategoryId);
    }
}
