using Nestly.Domain;

namespace Nestly.Application;

public interface ICityPricingPolicyRepository : IRepository<CityPricingPolicy>
{
    Task<CityPricingPolicy?> GetByCityAsync(Guid cityId);
}
