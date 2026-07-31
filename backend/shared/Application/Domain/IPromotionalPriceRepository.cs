using Nestly.Domain;

namespace Nestly.Application;

public interface IPromotionalPriceRepository : IRepository<PromotionalPrice>
{
    /// <summary>Promotional prices, optionally filtered by service, for the admin pricing screens (SRS 12.8.1).</summary>
    Task<IReadOnlyList<PromotionalPrice>> ListAsync(Guid? serviceId);
}
