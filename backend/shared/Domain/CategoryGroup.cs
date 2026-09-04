using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// An optional, purely visual section header for a subset of a parent
/// <see cref="Category"/>'s direct subcategories (e.g. "Large appliances" /
/// "Other appliances" under "AC &amp; Appliance Repair"). Mirrors
/// <see cref="ServiceGroup"/>'s shape exactly - a plain child entity with no
/// lifecycle or invariant of its own beyond its parent category's existence
/// - except this groups a category's subcategories rather than its services.
///
/// Purely additive: a subcategory whose <see cref="Category.CategoryGroupId"/>
/// is never set stays ungrouped and renders directly under its parent
/// exactly as before this entity existed. Assigning it to a group clusters
/// it under that group's heading instead. Nothing forces every parent
/// category to use groups.
/// </summary>
public class CategoryGroup : Entity<Guid>
{
    /// <summary>The parent category whose subcategory listing this group organizes - not the subcategories themselves.</summary>
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    protected CategoryGroup() { }

    public CategoryGroup(Guid id, Guid categoryId, string name) : base(id)
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
