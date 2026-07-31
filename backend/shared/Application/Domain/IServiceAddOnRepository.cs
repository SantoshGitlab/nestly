using Nestly.Domain;

namespace Nestly.Application;

public interface IServiceAddOnRepository : IRepository<ServiceAddOn>
{
    /// <summary>Active add-ons under a service, ordered for display.</summary>
    Task<IReadOnlyList<ServiceAddOn>> ListActiveByServiceAsync(Guid serviceId);

    /// <summary>Every add-on regardless of active status, optionally filtered by service, for the admin add-on pricing screen (SRS 12.8.1).</summary>
    Task<IReadOnlyList<ServiceAddOn>> ListAllAsync(Guid? serviceId);
}
