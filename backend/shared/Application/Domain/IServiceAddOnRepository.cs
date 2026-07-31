using Nestly.Domain;

namespace Nestly.Application;

public interface IServiceAddOnRepository : IRepository<ServiceAddOn>
{
    /// <summary>Active add-ons under a service, ordered for display.</summary>
    Task<IReadOnlyList<ServiceAddOn>> ListActiveByServiceAsync(Guid serviceId);

    /// <summary>
    /// Every add-on regardless of active status, optionally filtered to one
    /// service, ordered for the admin add-on management (SRS 12.7.1) and
    /// add-on pricing (SRS 12.8.1) screens.
    /// </summary>
    Task<IReadOnlyList<ServiceAddOn>> ListAllAsync(Guid? serviceId);
}
