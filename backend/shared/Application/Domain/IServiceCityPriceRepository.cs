using Nestly.Domain;

namespace Nestly.Application;

public interface IServiceCityPriceRepository : IRepository<ServiceCityPrice>
{
    Task<ServiceCityPrice?> GetForServiceAndCityAsync(Guid serviceId, Guid cityId);
}
