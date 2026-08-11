using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Groups a subset of a <see cref="Service"/>'s <see cref="ServiceAddOn"/>s
/// under one name with a selection rule (Phase 3 catalog redesign - e.g.
/// "Choose a detergent" as a pick-one group). Purely additive: an add-on
/// whose <see cref="ServiceAddOn.GroupId"/> is never set stays ungrouped and
/// renders/behaves exactly as before this entity existed. Shaped as a plain
/// child entity of Service, matching <see cref="ServiceMedia"/>/<see cref="ServiceFaq"/>'s
/// convention rather than an aggregate root - it has no lifecycle or
/// invariant of its own beyond its parent service's existence and its own
/// selection-rule fields.
/// </summary>
public class ServiceAddOnGroup : Entity<Guid>
{
    public Guid ServiceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AddOnGroupSelectionType SelectionType { get; private set; }
    public int MinSelect { get; private set; }
    public int? MaxSelect { get; private set; }
    public int SortOrder { get; private set; }

    protected ServiceAddOnGroup() { }

    public ServiceAddOnGroup(Guid id, Guid serviceId, string name, AddOnGroupSelectionType selectionType) : base(id)
    {
        ServiceId = serviceId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SelectionType = selectionType;
        MinSelect = 0;
        MaxSelect = selectionType == AddOnGroupSelectionType.Single ? 1 : null;
        SortOrder = 0;
    }

    public void SetServiceId(Guid serviceId) => ServiceId = serviceId;
    public void SetName(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void SetSelectionType(AddOnGroupSelectionType selectionType) => SelectionType = selectionType;

    public void SetSelectionRule(int minSelect, int? maxSelect)
    {
        if (minSelect < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minSelect), "MinSelect cannot be negative.");
        }

        if (maxSelect is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSelect), "MaxSelect must be at least 1 when set.");
        }

        if (maxSelect is int max && minSelect > max)
        {
            throw new ArgumentException("MinSelect cannot exceed MaxSelect.", nameof(minSelect));
        }

        if (SelectionType == AddOnGroupSelectionType.Single && maxSelect is int singleMax && singleMax > 1)
        {
            throw new ArgumentException("A Single-selection group's MaxSelect cannot exceed 1.", nameof(maxSelect));
        }

        MinSelect = minSelect;
        MaxSelect = maxSelect;
    }

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
