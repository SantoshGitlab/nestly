using Nestly.Domain;

namespace Nestly.Application;

public interface IServiceCityPriceRepository : IRepository<ServiceCityPrice>
{
    Task<ServiceCityPrice?> GetForServiceAndCityAsync(Guid serviceId, Guid cityId);

    /// <summary>City price overrides, optionally filtered by service and/or city, for the admin pricing screens (SRS 12.8.1).</summary>
    Task<IReadOnlyList<ServiceCityPrice>> ListAsync(Guid? serviceId, Guid? cityId);
}
