using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Admin CRUD over a service's gallery images (SRS 12.6.2 "Gallery images").</summary>
public interface IServiceMediaRepository
{
    Task AddAsync(ServiceMedia entity);
    Task DeleteAsync(ServiceMedia entity);
    Task<ServiceMedia?> GetByIdAsync(Guid id);

    /// <summary>Gallery images for a service.</summary>
    Task<IReadOnlyList<ServiceMedia>> ListByServiceAsync(Guid serviceId);
}
