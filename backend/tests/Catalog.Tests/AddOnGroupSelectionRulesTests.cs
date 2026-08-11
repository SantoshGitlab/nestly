using FluentAssertions;
using Nestly.Application.Pricing;
using Nestly.Domain;

namespace Nestly.Catalog.Tests;

/// <summary>Covers the Phase 3 catalog redesign's AddOnGroupSelectionRules pure validation helper, in isolation from PriceCalculationService.</summary>
public sealed class AddOnGroupSelectionRulesTests
{
    private static ServiceAddOn Grouped(Guid groupId, string name = "Add-on") =>
        Build(new ServiceAddOn(Guid.NewGuid(), Guid.NewGuid(), name, 50m), groupId);

    private static ServiceAddOn Build(ServiceAddOn addOn, Guid groupId)
    {
        addOn.SetGroupId(groupId);
        return addOn;
    }

    [Fact]
    public void Ungrouped_addons_are_never_checked()
    {
        var ungrouped = new ServiceAddOn(Guid.NewGuid(), Guid.NewGuid(), "Standalone", 20m);

        var result = AddOnGroupSelectionRules.Validate([ungrouped, ungrouped], new Dictionary<Guid, ServiceAddOnGroup>());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Two_selections_from_a_single_selection_group_are_rejected()
    {
        var groupId = Guid.NewGuid();
        var group = new ServiceAddOnGroup(groupId, Guid.NewGuid(), "Detergent", AddOnGroupSelectionType.Single);
        var addOnA = Grouped(groupId, "Powder");
        var addOnB = Grouped(groupId, "Liquid");

        var result = AddOnGroupSelectionRules.Validate([addOnA, addOnB], new Dictionary<Guid, ServiceAddOnGroup> { [groupId] = group });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.AddOnGroupSingleSelectionViolated");
    }

    [Fact]
    public void One_selection_from_a_single_selection_group_is_accepted()
    {
        var groupId = Guid.NewGuid();
        var group = new ServiceAddOnGroup(groupId, Guid.NewGuid(), "Detergent", AddOnGroupSelectionType.Single);
        var addOn = Grouped(groupId);

        var result = AddOnGroupSelectionRules.Validate([addOn], new Dictionary<Guid, ServiceAddOnGroup> { [groupId] = group });

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Selections_exceeding_a_multiple_selection_groups_max_select_are_rejected()
    {
        var groupId = Guid.NewGuid();
        var group = new ServiceAddOnGroup(groupId, Guid.NewGuid(), "Extras", AddOnGroupSelectionType.Multiple);
        group.SetSelectionRule(minSelect: 0, maxSelect: 2);
        var addOns = new[] { Grouped(groupId, "A"), Grouped(groupId, "B"), Grouped(groupId, "C") };

        var result = AddOnGroupSelectionRules.Validate(addOns, new Dictionary<Guid, ServiceAddOnGroup> { [groupId] = group });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.AddOnGroupMaxSelectExceeded");
    }

    [Fact]
    public void Fewer_selections_than_a_groups_min_select_are_rejected()
    {
        var groupId = Guid.NewGuid();
        var group = new ServiceAddOnGroup(groupId, Guid.NewGuid(), "Required extras", AddOnGroupSelectionType.Multiple);
        group.SetSelectionRule(minSelect: 2, maxSelect: null);
        var addOn = Grouped(groupId);

        var result = AddOnGroupSelectionRules.Validate([addOn], new Dictionary<Guid, ServiceAddOnGroup> { [groupId] = group });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Pricing.AddOnGroupMinSelectNotMet");
    }

    [Fact]
    public void Selections_from_an_unrelated_group_dont_affect_validation_of_another_group()
    {
        var singleGroupId = Guid.NewGuid();
        var multiGroupId = Guid.NewGuid();
        var singleGroup = new ServiceAddOnGroup(singleGroupId, Guid.NewGuid(), "Detergent", AddOnGroupSelectionType.Single);
        var multiGroup = new ServiceAddOnGroup(multiGroupId, Guid.NewGuid(), "Extras", AddOnGroupSelectionType.Multiple);
        var selections = new[] { Grouped(singleGroupId), Grouped(multiGroupId), Grouped(multiGroupId) };

        var result = AddOnGroupSelectionRules.Validate(
            selections,
            new Dictionary<Guid, ServiceAddOnGroup> { [singleGroupId] = singleGroup, [multiGroupId] = multiGroup });

        result.IsSuccess.Should().BeTrue();
    }
}
