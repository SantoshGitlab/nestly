using Nestly.Domain;

namespace Nestly.Application;

public interface IServiceAddOnRepository : IRepository<ServiceAddOn>
{
    /// <summary>Active add-ons under a service, ordered for display.</summary>
    Task<IReadOnlyList<ServiceAddOn>> ListActiveByServiceAsync(Guid serviceId);

    /// <summary>
    /// Active add-ons across every service in <paramref name="serviceIds"/>,
    /// in one query (task 136a) - the batch counterpart to
    /// <see cref="ListActiveByServiceAsync"/> for callers that need add-ons
    /// for a whole list of services (e.g. a category detail page) and would
    /// otherwise call it once per service in a loop.
    /// </summary>
    Task<IReadOnlyList<ServiceAddOn>> ListActiveByServiceIdsAsync(IReadOnlyCollection<Guid> serviceIds);

    /// <summary>
    /// Every add-on regardless of active status, optionally filtered to one
    /// service, ordered for the admin add-on management (SRS 12.7.1) and
    /// add-on pricing (SRS 12.8.1) screens.
    /// </summary>
    Task<IReadOnlyList<ServiceAddOn>> ListAllAsync(Guid? serviceId);

    /// <summary>Whether any add-on still references this group (Phase 3 catalog redesign) - guards <see cref="IServiceAddOnGroupManagementService.DeleteAsync"/> against orphaning a group's members.</summary>
    Task<bool> ExistsByGroupIdAsync(Guid groupId);
}
