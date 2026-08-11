using FluentAssertions;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Covers the Phase 3 catalog redesign's ServiceAddOnGroup entity: construction, selection-rule invariants.</summary>
public sealed class ServiceAddOnGroupAggregateTests
{
    [Fact]
    public void A_new_single_selection_group_defaults_max_select_to_one()
    {
        var group = new ServiceAddOnGroup(Guid.NewGuid(), Guid.NewGuid(), "Choose a detergent", AddOnGroupSelectionType.Single);

        group.MinSelect.Should().Be(0);
        group.MaxSelect.Should().Be(1);
    }

    [Fact]
    public void A_new_multiple_selection_group_defaults_to_an_unbounded_max_select()
    {
        var group = new ServiceAddOnGroup(Guid.NewGuid(), Guid.NewGuid(), "Optional extras", AddOnGroupSelectionType.Multiple);

        group.MaxSelect.Should().BeNull();
    }

    [Fact]
    public void SetSelectionRule_rejects_a_max_select_below_min_select()
    {
        var group = new ServiceAddOnGroup(Guid.NewGuid(), Guid.NewGuid(), "Extras", AddOnGroupSelectionType.Multiple);

        var act = () => group.SetSelectionRule(minSelect: 3, maxSelect: 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetSelectionRule_rejects_a_max_select_above_one_for_a_single_selection_group()
    {
        var group = new ServiceAddOnGroup(Guid.NewGuid(), Guid.NewGuid(), "Detergent", AddOnGroupSelectionType.Single);

        var act = () => group.SetSelectionRule(minSelect: 0, maxSelect: 2);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetSelectionRule_accepts_a_valid_bounded_multiple_selection_rule()
    {
        var group = new ServiceAddOnGroup(Guid.NewGuid(), Guid.NewGuid(), "Extras", AddOnGroupSelectionType.Multiple);

        group.SetSelectionRule(minSelect: 1, maxSelect: 3);

        group.MinSelect.Should().Be(1);
        group.MaxSelect.Should().Be(3);
    }
}
