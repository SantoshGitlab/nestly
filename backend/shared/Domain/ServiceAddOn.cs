using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

public class ServiceAddOn : Entity<Guid>
{
    public Guid ServiceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }

    protected ServiceAddOn() { }

    public ServiceAddOn(Guid id, Guid serviceId, string name, decimal price) : base(id)
    {
        ServiceId = serviceId;
        Name = name;
        Price = price;
    }

    public void SetServiceId(Guid serviceId) => ServiceId = serviceId;
    public void SetName(string name) => Name = name;
    public void SetPrice(decimal price) => Price = price;
}
