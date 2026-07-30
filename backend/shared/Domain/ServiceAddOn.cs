using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

public class ServiceAddOn : AggregateRoot<Guid>
{
    public Guid ServiceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }

    protected ServiceAddOn() { }

    public ServiceAddOn(Guid id, Guid serviceId, string name, decimal price) : base(id)
    {
        ServiceId = serviceId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
        IsActive = true;
        SortOrder = 0;
    }

    public void SetServiceId(Guid serviceId) => ServiceId = serviceId;
    public void SetName(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void SetPrice(decimal price) => Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
    public void SetDescription(string? description) => Description = description;
    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
