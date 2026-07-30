using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// City-specific override of a service's base price (SRS 12.8.1 "City-wise
/// price"). Absence of a row for a (service, city) pair means the service's
/// own Price applies everywhere in that city.
/// </summary>
public class ServiceCityPrice : Entity<Guid>
{
    public Guid ServiceId { get; private set; }
    public Guid CityId { get; private set; }
    public decimal Price { get; private set; }

    protected ServiceCityPrice() { }

    public ServiceCityPrice(Guid id, Guid serviceId, Guid cityId, decimal price) : base(id)
    {
        ServiceId = serviceId;
        CityId = cityId;
        SetPrice(price);
    }

    public void SetPrice(decimal price)
    {
        Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
    }
}
