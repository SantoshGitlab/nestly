using Nestly.Domain;

namespace Nestly.Application;

public interface IProviderLocationPingRepository
{
    Task AddAsync(ProviderLocationPing entity);

    /// <summary>The most recent fix recorded for a booking, or null if none has arrived yet.</summary>
    Task<ProviderLocationPing?> GetLatestForBookingAsync(Guid bookingId);

    /// <summary>The booking's full trail, oldest fix first - the order a route is drawn in.</summary>
    Task<IReadOnlyList<ProviderLocationPing>> GetTrailForBookingAsync(Guid bookingId);
}
