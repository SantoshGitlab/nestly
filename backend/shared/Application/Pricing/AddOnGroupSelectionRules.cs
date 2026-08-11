using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Pricing;

/// <summary>
/// Validates a set of selected add-ons against their <see cref="ServiceAddOnGroup"/>'s
/// selection rule (Phase 3 catalog redesign) - e.g. rejecting two selections
/// from a pick-one group. Pure (no I/O): callers resolve the selected
/// add-ons and their groups first, this only checks the counts. An add-on
/// with no group (today's default) is never checked here - it has no
/// selection rule to violate.
/// </summary>
public static class AddOnGroupSelectionRules
{
    public static Result Validate(
        IReadOnlyList<ServiceAddOn> selectedAddOns,
        IReadOnlyDictionary<Guid, ServiceAddOnGroup> groupsById)
    {
        foreach (var groupSelections in selectedAddOns.Where(a => a.GroupId is not null).GroupBy(a => a.GroupId!.Value))
        {
            if (!groupsById.TryGetValue(groupSelections.Key, out var group))
            {
                continue;
            }

            int selectedCount = groupSelections.Select(a => a.Id).Distinct().Count();

            if (group.SelectionType == AddOnGroupSelectionType.Single && selectedCount > 1)
            {
                return Result.Failure(Error.Validation(
                    "Pricing.AddOnGroupSingleSelectionViolated",
                    $"Only one add-on may be selected from \"{group.Name}\"."));
            }

            if (group.MaxSelect is int max && selectedCount > max)
            {
                return Result.Failure(Error.Validation(
                    "Pricing.AddOnGroupMaxSelectExceeded",
                    $"At most {max} add-on(s) may be selected from \"{group.Name}\"."));
            }

            if (group.MinSelect > 0 && selectedCount < group.MinSelect)
            {
                return Result.Failure(Error.Validation(
                    "Pricing.AddOnGroupMinSelectNotMet",
                    $"At least {group.MinSelect} add-on(s) must be selected from \"{group.Name}\"."));
            }
        }

        return Result.Success();
    }
}
