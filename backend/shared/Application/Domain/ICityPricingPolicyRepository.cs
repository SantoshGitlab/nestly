using Nestly.Domain;

namespace Nestly.Application;

public interface ICityPricingPolicyRepository : IRepository<CityPricingPolicy>
{
    Task<CityPricingPolicy?> GetByCityAsync(Guid cityId);

    /// <summary>Every configured city pricing policy, for the admin tax/fee management screen (SRS 12.8.1).</summary>
    Task<IReadOnlyList<CityPricingPolicy>> ListAsync();
}
