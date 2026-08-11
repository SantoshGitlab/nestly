using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// An optional, purely visual section header for a subset of a
/// <see cref="Category"/>'s services (e.g. "Super saver packages",
/// "Repair &amp; gas refill" under an appliance-level category such as
/// "AC"). Mirrors <see cref="ServiceAddOnGroup"/>'s shape - a plain child
/// entity with no lifecycle or invariant of its own beyond its parent
/// category's existence - except this groups a category's services rather
/// than a service's add-ons, and carries its own <see cref="IsActive"/>
/// flag since admins need to hide a whole section without touching the
/// individual services beneath it.
///
/// Purely additive: a service whose <see cref="Service.ServiceGroupId"/> is
/// never set stays ungrouped and renders directly under its appliance
/// exactly as before this entity existed (Model B). Assigning it to a
/// group renders it under that group's header instead (Model A). Nothing
/// forces every appliance to use groups - "AC" needs them, "Washing
/// Machine" doesn't, and both are valid.
/// </summary>
public class ServiceGroup : Entity<Guid>
{
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    protected ServiceGroup() { }

    public ServiceGroup(Guid id, Guid categoryId, string name) : base(id)
    {
        CategoryId = categoryId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IsActive = true;
        SortOrder = 0;
    }

    public void SetCategoryId(Guid categoryId) => CategoryId = categoryId;
    public void SetName(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
