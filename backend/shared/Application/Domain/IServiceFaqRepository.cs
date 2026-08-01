using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Read access to a service's FAQ entries (SRS 11.6.1 "FAQs", task 52d).</summary>
public interface IServiceFaqRepository
{
    /// <summary>A service's FAQs, ordered for display.</summary>
    Task<IReadOnlyList<ServiceFaq>> ListByServiceAsync(Guid serviceId);
}
