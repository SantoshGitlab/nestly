using Nesty;

namespace Nesty.Domain;

public class ServiceAddOn : Entity<Guid>
{
    public Guid ServiceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }

    protected ServiceAddOn() { }

    public ServiceAddOn(Guid id, Guid serviceId, string name, decimal price) : base(id)
    {
        ServiceId = serviceId;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
    }

    public void SetServiceId(Guid serviceId) => ServiceId = serviceId;
    public void SetName(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
    public void SetPrice(decimal price) => Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
}
