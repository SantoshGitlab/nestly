using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// Serviceability mapping: whether a category is active in a given city
/// (SRS 12.9.2). City is the level category-serviceability is mapped against.
/// Deactivating a mapping is how admins apply a temporary suspension or
/// blackout for that category in that city, without deleting the record.
/// </summary>
public class CategoryCityMapping : Entity<Guid>
{
    public Guid CategoryId { get; private set; }
    public Guid CityId { get; private set; }
    public bool IsActive { get; private set; }

    protected CategoryCityMapping() { }

    public CategoryCityMapping(Guid id, Guid categoryId, Guid cityId) : base(id)
    {
        CategoryId = categoryId;
        CityId = cityId;
        IsActive = true;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
