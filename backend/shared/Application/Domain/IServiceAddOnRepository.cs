using Nestly.Domain;

namespace Nestly.Application;

public interface IServiceAddOnRepository : IRepository<ServiceAddOn>
{
    /// <summary>Active add-ons under a service, ordered for display.</summary>
    Task<IReadOnlyList<ServiceAddOn>> ListActiveByServiceAsync(Guid serviceId);
}
